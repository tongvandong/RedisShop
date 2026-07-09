using Microsoft.AspNetCore.Mvc;
using RedisDemo.Api.Dtos;
using RedisDemo.Api.Repositories;
using StackExchange.Redis;

namespace RedisDemo.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthRepository _authRepository;
    private readonly RedisRepository _redisRepository;

    public AuthController(AuthRepository authRepository, RedisRepository redisRepository)
    {
        _authRepository = authRepository;
        _redisRepository = redisRepository;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Vui lòng nhập username và password." });
        }

        try
        {
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var usernamePart = NormalizeRateLimitPart(request.Username);
            var rateLimit = await _redisRepository.CheckRateLimitAsync($"login:ip:{clientIp}:user:{usernamePart}");
            Response.Headers["X-RateLimit-Limit"] = rateLimit.Limit.ToString();
            Response.Headers["X-RateLimit-Count"] = rateLimit.Count.ToString();
            Response.Headers["X-RateLimit-Ttl"] = rateLimit.Ttl.ToString();

            if (!rateLimit.Allowed)
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new
                {
                    message = "Bạn đăng nhập quá nhiều lần. Vui lòng thử lại sau.",
                    rateLimit
                });
            }
        }
        catch (RedisException)
        {
            return RedisUnavailable("Redis đang offline nên chức năng đăng nhập/session tạm thời chưa sử dụng được.");
        }

        var user = await _authRepository.LoginAsync(request);
        if (user is null)
        {
            return Unauthorized(new { message = "Username hoặc password không đúng." });
        }

        try
        {
            await _redisRepository.CreateSessionAsync(user);
            return Ok(user);
        }
        catch (RedisException)
        {
            return RedisUnavailable("Redis đang offline nên không thể tạo session đăng nhập.");
        }
    }

    [HttpGet("me")]
    public async Task<ActionResult<SessionUserDto>> Me()
    {
        try
        {
            var session = await _redisRepository.GetSessionAsync(ReadSessionId());
            if (session is null)
            {
                return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn hoặc không hợp lệ." });
            }

            return Ok(session);
        }
        catch (RedisException)
        {
            return RedisUnavailable("Redis đang offline nên không thể kiểm tra session.");
        }
    }

    [HttpPost("logout")]
    public async Task<ActionResult> Logout(LogoutRequest request)
    {
        var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
            ? ReadSessionId()
            : request.SessionId;

        try
        {
            await _redisRepository.DeleteSessionAsync(sessionId);
        }
        catch (RedisException)
        {
            Response.Headers["X-Redis-Status"] = "OFFLINE";
        }

        return NoContent();
    }

    private string ReadSessionId()
    {
        return Request.Headers.TryGetValue("X-Session-Id", out var value)
            ? value.ToString()
            : "";
    }

    private static string NormalizeRateLimitPart(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized)
            ? "unknown"
            : normalized.Replace(":", "_").Replace(" ", "_");
    }

    private ObjectResult RedisUnavailable(string message)
    {
        return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message });
    }
}

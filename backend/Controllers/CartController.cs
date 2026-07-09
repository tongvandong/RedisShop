using Microsoft.AspNetCore.Mvc;
using RedisDemo.Api.Dtos;
using RedisDemo.Api.Repositories;
using StackExchange.Redis;

namespace RedisDemo.Api.Controllers;

[ApiController]
[Route("api/users/{userId:long}/cart")]
public sealed class CartController : ControllerBase
{
    private readonly RedisRepository _redisRepository;

    public CartController(RedisRepository redisRepository)
    {
        _redisRepository = redisRepository;
    }

    [HttpGet]
    public async Task<ActionResult<List<CartItemDto>>> GetCart(long userId)
    {
        var sessionResult = await TrySessionMatchesUserAsync(userId);
        if (sessionResult.RedisOffline)
        {
            return RedisUnavailable();
        }

        if (!sessionResult.Matches)
        {
            return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ cho giỏ hàng này." });
        }

        try
        {
            return Ok(await _redisRepository.GetCartAsync(userId));
        }
        catch (RedisException)
        {
            return RedisUnavailable();
        }
    }

    [HttpPut("items/{productId:long}")]
    public async Task<ActionResult<List<CartItemDto>>> SaveCartItem(
        long userId,
        long productId,
        SaveCartItemRequest request)
    {
        var sessionResult = await TrySessionMatchesUserAsync(userId);
        if (sessionResult.RedisOffline)
        {
            return RedisUnavailable();
        }

        if (!sessionResult.Matches)
        {
            return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ cho giỏ hàng này." });
        }

        request.ProductId = productId;
        try
        {
            var cart = await _redisRepository.SaveCartItemAsync(userId, request);
            return Ok(cart);
        }
        catch (RedisException)
        {
            return RedisUnavailable();
        }
    }

    [HttpDelete]
    public async Task<ActionResult> ClearCart(long userId)
    {
        var sessionResult = await TrySessionMatchesUserAsync(userId);
        if (sessionResult.RedisOffline)
        {
            return RedisUnavailable();
        }

        if (!sessionResult.Matches)
        {
            return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ cho giỏ hàng này." });
        }

        try
        {
            await _redisRepository.ClearCartAsync(userId);
            return NoContent();
        }
        catch (RedisException)
        {
            return RedisUnavailable();
        }
    }

    private async Task<(bool Matches, bool RedisOffline)> TrySessionMatchesUserAsync(long userId)
    {
        try
        {
            var sessionId = Request.Headers.TryGetValue("X-Session-Id", out var value)
                ? value.ToString()
                : "";
            var session = await _redisRepository.GetSessionAsync(sessionId);
            return (session is not null && session.UserId == userId, false);
        }
        catch (RedisException)
        {
            return (false, true);
        }
    }

    private ObjectResult RedisUnavailable()
    {
        return StatusCode(StatusCodes.Status503ServiceUnavailable, new
        {
            message = "Redis đang offline nên giỏ hàng tạm thời chưa sử dụng được. Danh sách sản phẩm vẫn lấy từ MySQL."
        });
    }
}

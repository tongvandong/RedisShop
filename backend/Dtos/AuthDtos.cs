namespace RedisDemo.Api.Dtos;

public sealed class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class LoginResponse
{
    public long UserId { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "";
    public string SessionId { get; set; } = "";
    public int SessionTtlSeconds { get; set; }
}

public sealed class LogoutRequest
{
    public string SessionId { get; set; } = "";
}

public sealed class SessionUserDto
{
    public long UserId { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "";
    public long Ttl { get; set; }
}

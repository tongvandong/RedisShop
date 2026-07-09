using MySqlConnector;
using RedisDemo.Api.Data;
using RedisDemo.Api.Dtos;

namespace RedisDemo.Api.Repositories;

public sealed class AuthRepository
{
    private readonly MySqlConnectionFactory _connectionFactory;

    public AuthRepository(MySqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        const string sql = """
            SELECT id, username, full_name, role, password_hash
            FROM users
            WHERE username = @username AND is_active = TRUE
            LIMIT 1;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@username", request.Username.Trim());

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var storedPassword = reader.GetString("password_hash");
        if (!PasswordMatches(request.Password, storedPassword))
        {
            return null;
        }

        var userId = reader.GetInt64("id");
        var username = reader.GetString("username");

        return new LoginResponse
        {
            UserId = userId,
            Username = username,
            FullName = reader.GetString("full_name"),
            Role = reader.GetString("role"),
            SessionId = $"session:{Guid.NewGuid():N}"[..20],
            SessionTtlSeconds = 1800
        };
    }

    private static bool PasswordMatches(string password, string storedPassword)
    {
        if (storedPassword == password)
        {
            return true;
        }

        if (storedPassword == "demo-password-123456" && password == "123456")
        {
            return true;
        }

        if (storedPassword == "demo-password-admin" && password == "admin")
        {
            return true;
        }

        return false;
    }
}

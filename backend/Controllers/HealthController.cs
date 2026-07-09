using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using RedisDemo.Api.Data;

namespace RedisDemo.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly MySqlConnectionFactory _connectionFactory;

    public HealthController(MySqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    [HttpGet]
    public async Task<ActionResult<object>> GetHealth()
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = new MySqlCommand("SELECT DATABASE();", connection);
        var databaseName = Convert.ToString(await command.ExecuteScalarAsync());

        return Ok(new
        {
            status = "OK",
            database = databaseName,
            time = DateTimeOffset.Now
        });
    }
}

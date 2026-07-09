namespace RedisDemo.Api.Data;

public sealed class RedisOptions
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 6379;
    public string Password { get; set; } = "";
    public int Database { get; set; }
    public RedisSentinelOptions Sentinel { get; set; } = new();
    public RedisDirectOptions Cache { get; set; } = new();
    public RedisDirectOptions Persistence { get; set; } = new();
    public int ProductsCacheSeconds { get; set; } = 60;
    public int ProductDetailCacheSeconds { get; set; } = 300;
    public int SessionSeconds { get; set; } = 1800;
    public int CartSeconds { get; set; } = 1800;
    public int RateLimitSeconds { get; set; } = 60;
    public int RateLimitMaxRequests { get; set; } = 5;
}

public sealed class RedisSentinelOptions
{
    public bool Enabled { get; set; }
    public string ServiceName { get; set; } = "mymaster";
    public List<RedisEndpointOptions> Endpoints { get; set; } = [];
}

public sealed class RedisDirectOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string Password { get; set; } = "";
    public int Database { get; set; }
}

public sealed class RedisEndpointOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 26379;
}

namespace RedisDemo.Api.Dtos;

public sealed class RedisPingResponse
{
    public string Status { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string Message { get; set; } = "";
}

public sealed class RedisOverviewResponse
{
    public bool Online { get; set; }
    public string Endpoint { get; set; } = "";
    public long TotalKeys { get; set; }
    public string UsedMemoryHuman { get; set; } = "";
    public int ConnectedClients { get; set; }
    public TimeSpan Uptime { get; set; }
    public CacheMonitorDto Cache { get; set; } = new();
    public int ActiveSessions { get; set; }
    public int ActiveCarts { get; set; }
    public int RateLimitedClients { get; set; }
    public long StreamLength { get; set; }
}

public sealed class RedisDashboardDetailsResponse
{
    public List<SessionMonitorDto> Sessions { get; set; } = [];
    public List<CartMonitorDto> Carts { get; set; } = [];
    public List<RateLimitMonitorDto> RateLimits { get; set; } = [];
    public StreamMonitorDto Stream { get; set; } = new();
}

public sealed class SessionMonitorDto
{
    public string Key { get; set; } = "";
    public long UserId { get; set; }
    public string Username { get; set; } = "";
    public string Role { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public long Ttl { get; set; }
}

public sealed class CartMonitorDto
{
    public string Key { get; set; } = "";
    public long UserId { get; set; }
    public int ItemCount { get; set; }
    public int TotalQuantity { get; set; }
    public decimal TotalAmount { get; set; }
    public long Ttl { get; set; }
    public List<CartItemDto> Items { get; set; } = [];
}

public sealed class RateLimitMonitorDto
{
    public string Key { get; set; } = "";
    public string Client { get; set; } = "";
    public long Count { get; set; }
    public int Limit { get; set; }
    public long Ttl { get; set; }
    public bool Blocked { get; set; }
}

public sealed class StreamMonitorDto
{
    public string Key { get; set; } = "stream:orders";
    public string ConsumerGroup { get; set; } = "order-workers";
    public long Length { get; set; }
    public long PendingCount { get; set; }
    public int ConsumerCount { get; set; }
    public string LastDeliveredId { get; set; } = "";
    public string WorkerStatus { get; set; } = "Idle";
    public string PendingSummary { get; set; } = "";
    public string GroupsSummary { get; set; } = "";
}

public sealed class RedisInfrastructureResponse
{
    public string Endpoint { get; set; } = "";
    public string ConnectionMode { get; set; } = "";
    public string ConfiguredEndpoint { get; set; } = "";
    public List<RedisNodeStatusDto> Nodes { get; set; } = [];
    public string Role { get; set; } = "";
    public string RedisVersion { get; set; } = "";
    public string Os { get; set; } = "";
    public string Mode { get; set; } = "";
    public string PersistenceRdb { get; set; } = "";
    public string PersistenceAof { get; set; } = "";
    public string ConnectedReplicas { get; set; } = "";
    public string MasterLinkStatus { get; set; } = "";
    public string UsedMemoryPeakHuman { get; set; } = "";
    public string MaxMemoryHuman { get; set; } = "";
    public string MaxMemoryPolicy { get; set; } = "";
    public string CacheEndpoint { get; set; } = "";
    public string CacheMaxMemory { get; set; } = "";
    public string CacheMaxMemoryPolicy { get; set; } = "";
    public string PersistenceEndpoint { get; set; } = "";
    public string PersistenceAppendOnly { get; set; } = "";
    public string PersistenceSave { get; set; } = "";
}

public sealed class RedisNodeStatusDto
{
    public string Name { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string SentinelEndpoint { get; set; } = "";
    public string Role { get; set; } = "";
    public string Status { get; set; } = "";
    public string Note { get; set; } = "";
    public string RedisVersion { get; set; } = "";
    public string Os { get; set; } = "";
    public string Mode { get; set; } = "";
    public string PersistenceRdb { get; set; } = "";
    public string PersistenceAof { get; set; } = "";
    public string ConnectedReplicas { get; set; } = "";
    public string MasterLinkStatus { get; set; } = "";
    public string UsedMemoryPeakHuman { get; set; } = "";
    public string MaxMemoryPolicy { get; set; } = "";
    public string MaxMemoryHuman { get; set; } = "";
}

public sealed class RedisPersistenceTestResponse
{
    public string Status { get; set; } = "";
    public long TotalKeys { get; set; }
    public int ExistingCount { get; set; }
    public int MissingCount { get; set; }
    public bool Pass { get; set; }
    public string Instruction { get; set; } = "";
    public DateTimeOffset CheckedAt { get; set; }
    public List<PersistenceTestKeyDto> Keys { get; set; } = [];
}

public sealed class PersistenceTestKeyDto
{
    public string Key { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Exists { get; set; }
    public long Ttl { get; set; }
    public string ValuePreview { get; set; } = "";
}

public sealed class CacheMonitorDto
{
    public string Key { get; set; } = "cache:products";
    public string LastSource { get; set; } = "UNKNOWN";
    public long Hit { get; set; }
    public long Miss { get; set; }
    public long Ttl { get; set; }
    public long LastDurationMs { get; set; }
    public long RedisDurationMs { get; set; }
    public long MySqlDurationMs { get; set; }
    public double Speedup { get; set; }
}

public sealed class CartItemDto
{
    public long ProductId { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public decimal Subtotal { get; set; }
}

public sealed class SaveCartItemRequest
{
    public long ProductId { get; set; }
    public int Quantity { get; set; }
}

public sealed class RateLimitResult
{
    public bool Allowed { get; set; }
    public long Count { get; set; }
    public int Limit { get; set; }
    public long Ttl { get; set; }
}

public sealed class RankingItemDto
{
    public long ProductId { get; set; }
    public string Name { get; set; } = "";
    public double Score { get; set; }
}

public sealed class StreamMessageDto
{
    public string Id { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string OrderCode { get; set; } = "";
    public string Status { get; set; } = "";
}

public sealed class PublishMessageRequest
{
    public string Channel { get; set; } = "notifications";
    public string Message { get; set; } = "";
}

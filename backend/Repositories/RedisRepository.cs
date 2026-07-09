using System.Text.Json;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using RedisDemo.Api.Data;
using RedisDemo.Api.Dtos;
using StackExchange.Redis;

namespace RedisDemo.Api.Repositories;

public sealed class RedisRepository
{
    private const string ProductsCacheKey = "cache:products";
    private const string CacheHitKey = "metrics:cache:products:hit";
    private const string CacheMissKey = "metrics:cache:products:miss";
    private const string CacheSourceKey = "metrics:cache:products:last-source";
    private const string CacheLastDurationKey = "metrics:cache:products:last-duration-ms";
    private const string CacheRedisDurationKey = "metrics:cache:products:redis-duration-ms";
    private const string CacheMySqlDurationKey = "metrics:cache:products:mysql-duration-ms";
    private const string ProductsCacheRebuildLockKey = "lock:cache:products:rebuild";
    private const string ProductDetailViewsKey = "ranking:product:views";
    private const string RankingProductsKey = "ranking:products";
    private const string RankingProcessedOrdersKey = "ranking:processed-orders";
    private const string OrdersStreamKey = "stream:orders";
    private const string OrdersConsumerGroup = "order-workers";
    private const string NotificationsChannel = "notifications";
    private static readonly RedisKey[] PersistenceTestKeys =
    [
        "persistence:test:cache",
        "persistence:test:session",
        "persistence:test:cart",
        "persistence:test:ranking",
        "persistence:test:stream"
    ];

    private readonly RedisConnectionFactory _connectionFactory;
    private readonly ProductRepository _productRepository;
    private readonly OrderRepository _orderRepository;
    private readonly RedisOptions _options;

    public RedisRepository(
        RedisConnectionFactory connectionFactory,
        ProductRepository productRepository,
        OrderRepository orderRepository,
        IOptions<RedisOptions> options)
    {
        _connectionFactory = connectionFactory;
        _productRepository = productRepository;
        _orderRepository = orderRepository;
        _options = options.Value;
    }

    public async Task<RedisPingResponse> PingAsync()
    {
        var db = _connectionFactory.GetDatabase();
        var result = await db.PingAsync();

        return new RedisPingResponse
        {
            Status = "OK",
            Host = _connectionFactory.GetCurrentRedisEndpoint().Host,
            Port = _connectionFactory.GetCurrentRedisEndpoint().Port,
            Message = $"PONG {result.TotalMilliseconds:0.##} ms via {_connectionFactory.GetConfiguredMode()}"
        };
    }

    public async Task<List<ProductDto>?> GetProductsFromCacheAsync(bool trackMetrics = true)
    {
        if (_connectionFactory.ShouldSkipOptionalRedis())
        {
            return null;
        }

        try
        {
            var db = _connectionFactory.GetCacheDatabase();
            var timer = Stopwatch.StartNew();
            var cached = await db.StringGetAsync(ProductsCacheKey);

            if (cached.IsNullOrEmpty)
            {
                if (trackMetrics)
                {
                    await db.StringIncrementAsync(CacheMissKey);
                }

                _connectionFactory.MarkOptionalRedisSuccess();
                return null;
            }

            var products = JsonSerializer.Deserialize<List<ProductDto>>(cached!) ?? [];
            timer.Stop();

            if (trackMetrics)
            {
                await db.StringIncrementAsync(CacheHitKey);
                await db.StringSetAsync(CacheSourceKey, "REDIS");
                await db.StringSetAsync(CacheLastDurationKey, timer.ElapsedMilliseconds);
                await db.StringSetAsync(CacheRedisDurationKey, timer.ElapsedMilliseconds);
            }

            var ttl = (int)Math.Max(0, (await db.KeyTimeToLiveAsync(ProductsCacheKey))?.TotalSeconds ?? 0);

            foreach (var product in products)
            {
                product.Source = "REDIS";
                product.Ttl = ttl;
            }

            _connectionFactory.MarkOptionalRedisSuccess();
            return products;
        }
        catch (RedisException)
        {
            _connectionFactory.MarkOptionalRedisFailure(TimeSpan.FromSeconds(20));
            return null;
        }
    }

    public async Task<ProductDto?> GetProductFromCacheAsync(long productId)
    {
        if (_connectionFactory.ShouldSkipOptionalRedis())
        {
            return null;
        }

        try
        {
            var db = _connectionFactory.GetCacheDatabase();
            var cached = await db.StringGetAsync(GetProductDetailCacheKey(productId));
            if (cached.IsNullOrEmpty)
            {
                return null;
            }

            var product = JsonSerializer.Deserialize<ProductDto>(cached!);
            if (product is null)
            {
                return null;
            }

            product.Source = "REDIS";
            product.Ttl = (int)Math.Max(0, (await db.KeyTimeToLiveAsync(GetProductDetailCacheKey(productId)))?.TotalSeconds ?? 0);
            await db.SortedSetIncrementAsync(ProductDetailViewsKey, productId.ToString(), 1);
            _connectionFactory.MarkOptionalRedisSuccess();
            return product;
        }
        catch (RedisException)
        {
            _connectionFactory.MarkOptionalRedisFailure(TimeSpan.FromSeconds(20));
            return null;
        }
    }

    public async Task SaveProductCacheAsync(ProductDto product)
    {
        product.Source = "MYSQL";
        product.Ttl = _options.ProductDetailCacheSeconds;

        if (_connectionFactory.ShouldSkipOptionalRedis())
        {
            return;
        }

        try
        {
            var db = _connectionFactory.GetCacheDatabase();
            await db.StringSetAsync(
                GetProductDetailCacheKey(product.Id),
                JsonSerializer.Serialize(product),
                TimeSpan.FromSeconds(_options.ProductDetailCacheSeconds));
            await db.SortedSetIncrementAsync(ProductDetailViewsKey, product.Id.ToString(), 1);
            _connectionFactory.MarkOptionalRedisSuccess();
        }
        catch (RedisException)
        {
            _connectionFactory.MarkOptionalRedisFailure(TimeSpan.FromSeconds(20));
        }
    }

    public async Task SaveProductsCacheAsync(List<ProductDto> products, long mySqlDurationMs, bool trackMetrics = true)
    {
        foreach (var product in products)
        {
            product.Source = "MYSQL";
            product.Ttl = _options.ProductsCacheSeconds;
        }

        if (_connectionFactory.ShouldSkipOptionalRedis())
        {
            return;
        }

        try
        {
            var db = _connectionFactory.GetCacheDatabase();
            var json = JsonSerializer.Serialize(products);
            await db.StringSetAsync(
                ProductsCacheKey,
                json,
                TimeSpan.FromSeconds(_options.ProductsCacheSeconds));
            if (trackMetrics)
            {
                await db.StringSetAsync(CacheSourceKey, "MYSQL");
                await db.StringSetAsync(CacheLastDurationKey, mySqlDurationMs);
                await db.StringSetAsync(CacheMySqlDurationKey, mySqlDurationMs);
            }

            _connectionFactory.MarkOptionalRedisSuccess();
        }
        catch (RedisException)
        {
            _connectionFactory.MarkOptionalRedisFailure(TimeSpan.FromSeconds(20));
        }
    }

    public async Task<string?> TryAcquireProductsCacheRebuildLockAsync()
    {
        if (_connectionFactory.ShouldSkipOptionalRedis())
        {
            return null;
        }

        try
        {
            var db = _connectionFactory.GetCacheDatabase();
            var token = Guid.NewGuid().ToString("N");
            var acquired = await db.StringSetAsync(
                ProductsCacheRebuildLockKey,
                token,
                TimeSpan.FromSeconds(5),
                When.NotExists);

            _connectionFactory.MarkOptionalRedisSuccess();
            return acquired ? token : null;
        }
        catch (RedisException)
        {
            _connectionFactory.MarkOptionalRedisFailure(TimeSpan.FromSeconds(20));
            return null;
        }
    }

    public async Task ReleaseProductsCacheRebuildLockAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        try
        {
            var db = _connectionFactory.GetCacheDatabase();
            const string script = """
                if redis.call('GET', KEYS[1]) == ARGV[1] then
                    return redis.call('DEL', KEYS[1])
                end
                return 0
                """;

            await db.ScriptEvaluateAsync(script, [ProductsCacheRebuildLockKey], [token]);
        }
        catch (RedisException)
        {
            _connectionFactory.MarkOptionalRedisFailure(TimeSpan.FromSeconds(20));
        }
    }

    public async Task<string?> TryAcquireProductCacheRebuildLockAsync(long productId)
    {
        if (_connectionFactory.ShouldSkipOptionalRedis())
        {
            return null;
        }

        try
        {
            var db = _connectionFactory.GetCacheDatabase();
            var token = Guid.NewGuid().ToString("N");
            var acquired = await db.StringSetAsync(
                GetProductDetailRebuildLockKey(productId),
                token,
                TimeSpan.FromSeconds(5),
                When.NotExists);

            _connectionFactory.MarkOptionalRedisSuccess();
            return acquired ? token : null;
        }
        catch (RedisException)
        {
            _connectionFactory.MarkOptionalRedisFailure(TimeSpan.FromSeconds(20));
            return null;
        }
    }

    public async Task ReleaseProductCacheRebuildLockAsync(long productId, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        try
        {
            var db = _connectionFactory.GetCacheDatabase();
            const string script = """
                if redis.call('GET', KEYS[1]) == ARGV[1] then
                    return redis.call('DEL', KEYS[1])
                end
                return 0
                """;

            await db.ScriptEvaluateAsync(script, [GetProductDetailRebuildLockKey(productId)], [token]);
        }
        catch (RedisException)
        {
            _connectionFactory.MarkOptionalRedisFailure(TimeSpan.FromSeconds(20));
        }
    }

    public async Task<ProductDto?> WaitForProductCacheAsync(long productId, TimeSpan timeout, TimeSpan interval)
    {
        if (_connectionFactory.ShouldSkipOptionalRedis())
        {
            return null;
        }

        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(interval);

            var product = await GetProductFromCacheAsync(productId);
            if (product is not null)
            {
                return product;
            }
        }

        return null;
    }

    public async Task<List<ProductDto>?> WaitForProductsCacheAsync(TimeSpan timeout, TimeSpan interval, bool trackMetrics = true)
    {
        if (_connectionFactory.ShouldSkipOptionalRedis())
        {
            return null;
        }

        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(interval);

            var products = await ReadProductsCacheWithoutMissAsync(trackMetrics);
            if (products is not null)
            {
                return products;
            }
        }

        return null;
    }

    private async Task<List<ProductDto>?> ReadProductsCacheWithoutMissAsync(bool trackMetrics)
    {
        try
        {
            var db = _connectionFactory.GetCacheDatabase();
            var timer = Stopwatch.StartNew();
            var cached = await db.StringGetAsync(ProductsCacheKey);
            if (cached.IsNullOrEmpty)
            {
                return null;
            }

            var products = JsonSerializer.Deserialize<List<ProductDto>>(cached!) ?? [];
            timer.Stop();

            if (trackMetrics)
            {
                await db.StringIncrementAsync(CacheHitKey);
                await db.StringSetAsync(CacheSourceKey, "REDIS");
                await db.StringSetAsync(CacheLastDurationKey, timer.ElapsedMilliseconds);
                await db.StringSetAsync(CacheRedisDurationKey, timer.ElapsedMilliseconds);
            }

            var ttl = (int)Math.Max(0, (await db.KeyTimeToLiveAsync(ProductsCacheKey))?.TotalSeconds ?? 0);
            foreach (var product in products)
            {
                product.Source = "REDIS";
                product.Ttl = ttl;
            }

            return products;
        }
        catch (RedisException)
        {
            _connectionFactory.MarkOptionalRedisFailure(TimeSpan.FromSeconds(20));
            return null;
        }
    }

    public async Task ClearProductsCacheAsync()
    {
        if (_connectionFactory.ShouldSkipOptionalRedis())
        {
            return;
        }

        try
        {
            var db = _connectionFactory.GetCacheDatabase();
            var keys = new List<RedisKey>
            {
                ProductsCacheKey,
                CacheLastDurationKey,
                CacheRedisDurationKey,
                CacheMySqlDurationKey
            };

            await foreach (var key in _connectionFactory.GetCacheServer().KeysAsync(pattern: "cache:product:*"))
            {
                keys.Add(key);
            }

            await db.KeyDeleteAsync([.. keys]);
            await db.StringSetAsync(CacheSourceKey, "CLEARED");
            _connectionFactory.MarkOptionalRedisSuccess();
        }
        catch (RedisException)
        {
            _connectionFactory.MarkOptionalRedisFailure(TimeSpan.FromSeconds(20));
        }
    }

    public async Task ClearProductCacheAsync(long productId)
    {
        if (_connectionFactory.ShouldSkipOptionalRedis())
        {
            return;
        }

        try
        {
            await _connectionFactory.GetCacheDatabase().KeyDeleteAsync(GetProductDetailCacheKey(productId));
            _connectionFactory.MarkOptionalRedisSuccess();
        }
        catch (RedisException)
        {
            _connectionFactory.MarkOptionalRedisFailure(TimeSpan.FromSeconds(20));
        }
    }

    public async Task<string> CreateSessionAsync(LoginResponse user)
    {
        var db = _connectionFactory.GetDatabase();
        var sessionKey = user.SessionId;
        user.SessionTtlSeconds = _options.SessionSeconds;

        var entries = new HashEntry[]
        {
            new("userId", user.UserId),
            new("username", user.Username),
            new("fullName", user.FullName),
            new("role", user.Role),
            new("createdAt", DateTimeOffset.Now.ToString("O"))
        };

        await db.HashSetAsync(sessionKey, entries);
        await db.KeyExpireAsync(sessionKey, TimeSpan.FromSeconds(_options.SessionSeconds));
        return sessionKey;
    }

    public async Task<SessionUserDto?> GetSessionAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || !sessionId.StartsWith("session:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var db = _connectionFactory.GetDatabase();
        var entries = await db.HashGetAllAsync(sessionId);
        if (entries.Length == 0)
        {
            return null;
        }

        await db.KeyExpireAsync(sessionId, TimeSpan.FromSeconds(_options.SessionSeconds));
        var ttl = await db.KeyTimeToLiveAsync(sessionId);
        var byName = entries.ToDictionary(entry => entry.Name.ToString(), entry => entry.Value.ToString());

        return new SessionUserDto
        {
            UserId = long.TryParse(byName.GetValueOrDefault("userId"), out var userId) ? userId : 0,
            Username = byName.GetValueOrDefault("username") ?? "",
            FullName = byName.GetValueOrDefault("fullName") ?? "",
            Role = byName.GetValueOrDefault("role") ?? "",
            Ttl = (long)Math.Max(0, ttl?.TotalSeconds ?? 0)
        };
    }

    public async Task DeleteSessionAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        await _connectionFactory.GetDatabase().KeyDeleteAsync(sessionId);
    }

    public async Task<List<CartItemDto>> GetCartAsync(long userId)
    {
        var db = _connectionFactory.GetDatabase();
        var key = GetCartKey(userId);
        var entries = await db.HashGetAllAsync(key);

        return entries
            .Select(entry => new CartItemDto
            {
                ProductId = long.Parse(entry.Name!),
                Quantity = (int)entry.Value
            })
            .OrderBy(item => item.ProductId)
            .ToList();
    }

    public async Task<List<CartItemDto>> SaveCartItemAsync(long userId, SaveCartItemRequest request)
    {
        var db = _connectionFactory.GetDatabase();
        var key = GetCartKey(userId);

        if (request.Quantity <= 0)
        {
            await db.HashDeleteAsync(key, request.ProductId.ToString());
        }
        else
        {
            await db.HashSetAsync(key, request.ProductId.ToString(), request.Quantity);
            await db.KeyExpireAsync(key, TimeSpan.FromSeconds(_options.CartSeconds));
        }

        return await GetCartAsync(userId);
    }

    public async Task ClearCartAsync(long userId)
    {
        await _connectionFactory.GetDatabase().KeyDeleteAsync(GetCartKey(userId));
    }

    public async Task<RateLimitResult> CheckRateLimitAsync(string name)
    {
        var db = _connectionFactory.GetDatabase();
        var key = $"rate:{name}";

        var count = await db.StringIncrementAsync(key);
        if (count == 1)
        {
            await db.KeyExpireAsync(key, TimeSpan.FromSeconds(_options.RateLimitSeconds));
        }

        var ttl = await db.KeyTimeToLiveAsync(key);
        return new RateLimitResult
        {
            Allowed = count <= _options.RateLimitMaxRequests,
            Count = count,
            Limit = _options.RateLimitMaxRequests,
            Ttl = (long)Math.Max(0, ttl?.TotalSeconds ?? 0)
        };
    }

    public async Task<bool> IncrementProductRankingForOrderAsync(long orderId, IEnumerable<CreateOrderItemRequest> items)
    {
        var itemList = items.Where(item => item.ProductId > 0 && item.Quantity > 0).ToList();
        if (orderId <= 0 || itemList.Count == 0)
        {
            return false;
        }

        var db = _connectionFactory.GetDatabase();
        var args = new List<RedisValue> { orderId.ToString() };
        foreach (var item in itemList)
        {
            args.Add(item.ProductId.ToString());
            args.Add(item.Quantity);
        }

        const string script = """
            local applied = redis.call('SADD', KEYS[1], ARGV[1])
            if applied == 0 then
                return 0
            end

            for index = 2, #ARGV, 2 do
                redis.call('ZINCRBY', KEYS[2], ARGV[index + 1], ARGV[index])
            end

            return 1
            """;

        var result = await db.ScriptEvaluateAsync(
            script,
            [RankingProcessedOrdersKey, RankingProductsKey],
            [.. args]);

        return (int)result == 1;
    }

    public async Task<List<RankingItemDto>> GetRankingAsync(List<ProductDto> products, int take = 5)
    {
        var db = _connectionFactory.GetDatabase();
        var entries = await db.SortedSetRangeByRankWithScoresAsync(
            RankingProductsKey,
            0,
            take - 1,
            Order.Descending);

        var byId = products.ToDictionary(product => product.Id);
        return entries.Select(entry =>
        {
            var productId = long.Parse(entry.Element!);
            return new RankingItemDto
            {
                ProductId = productId,
                Name = byId.TryGetValue(productId, out var product) ? product.Name : $"Product {productId}",
                Score = entry.Score
            };
        }).ToList();
    }

    public async Task<List<RankingItemDto>> GetProductViewRankingAsync(List<ProductDto> products, int take = 5)
    {
        var db = _connectionFactory.GetCacheDatabase();
        var entries = await db.SortedSetRangeByRankWithScoresAsync(
            ProductDetailViewsKey,
            0,
            take - 1,
            Order.Descending);

        var byId = products.ToDictionary(product => product.Id);
        return entries.Select(entry =>
        {
            var productId = long.Parse(entry.Element!);
            return new RankingItemDto
            {
                ProductId = productId,
                Name = byId.TryGetValue(productId, out var product) ? product.Name : $"Product {productId}",
                Score = entry.Score
            };
        }).ToList();
    }

    public async Task<string> AddOrderStreamMessageAsync(OrderCreatedResponse order)
    {
        var db = _connectionFactory.GetDatabase();
        var id = await db.StreamAddAsync(
            OrdersStreamKey,
            [
                new NameValueEntry("orderId", order.OrderId),
                new NameValueEntry("orderCode", order.OrderCode),
                new NameValueEntry("status", order.Status),
                new NameValueEntry("totalAmount", order.TotalAmount.ToString("0.##"))
            ]);

        return id!;
    }

    public async Task<List<StreamMessageDto>> GetOrderStreamMessagesAsync(int take = 20)
    {
        var db = _connectionFactory.GetDatabase();
        var entries = await db.StreamRangeAsync(OrdersStreamKey, count: take, messageOrder: Order.Descending);
        var streamMessages = entries.Select(entry => new StreamMessageDto
        {
            Id = entry.Id!,
            OrderId = GetStreamValue(entry, "orderId"),
            OrderCode = GetStreamValue(entry, "orderCode"),
            Status = GetStreamValue(entry, "status")
        }).ToList();

        var orderIds = streamMessages
            .Select(message => long.TryParse(message.OrderId, out var orderId) ? orderId : 0);
        var statuses = await _orderRepository.GetOrderStatusesAsync(orderIds);

        foreach (var message in streamMessages)
        {
            if (long.TryParse(message.OrderId, out var orderId) && statuses.TryGetValue(orderId, out var status))
            {
                message.Status = status;
            }
        }

        return streamMessages;
    }

    public async Task EnsureOrderConsumerGroupAsync()
    {
        var db = _connectionFactory.GetDatabase();
        try
        {
            await db.StreamCreateConsumerGroupAsync(
                OrdersStreamKey,
                OrdersConsumerGroup,
                "0-0",
                createStream: true);
        }
        catch (RedisServerException exception)
            when (exception.Message.Contains("BUSYGROUP", StringComparison.OrdinalIgnoreCase))
        {
            // Consumer group already exists. That is the normal case after the first run.
        }
    }

    public async Task<StreamEntry[]> ReadOrderStreamAsync(string consumerName, int count = 5)
    {
        var db = _connectionFactory.GetDatabase();
        return await db.StreamReadGroupAsync(
            OrdersStreamKey,
            OrdersConsumerGroup,
            consumerName,
            ">",
            count);
    }

    public async Task<StreamEntry[]> ClaimStaleOrderStreamMessagesAsync(
        string consumerName,
        long minIdleMilliseconds = 30000,
        int count = 5)
    {
        var db = _connectionFactory.GetDatabase();
        var pending = await db.StreamPendingMessagesAsync(
            OrdersStreamKey,
            OrdersConsumerGroup,
            count,
            RedisValue.Null,
            "-",
            "+");

        var staleMessageIds = pending
            .Where(message => message.IdleTimeInMilliseconds >= minIdleMilliseconds)
            .Select(message => message.MessageId)
            .ToArray();

        if (staleMessageIds.Length == 0)
        {
            return [];
        }

        return await db.StreamClaimAsync(
            OrdersStreamKey,
            OrdersConsumerGroup,
            consumerName,
            minIdleMilliseconds,
            staleMessageIds);
    }

    public async Task AckOrderStreamAsync(RedisValue messageId)
    {
        var db = _connectionFactory.GetDatabase();
        await db.StreamAcknowledgeAsync(OrdersStreamKey, OrdersConsumerGroup, messageId);
    }

    public long ReadOrderId(StreamEntry entry)
    {
        var orderId = GetStreamValue(entry, "orderId");
        return long.TryParse(orderId, out var value) ? value : 0;
    }

    public async Task<long> PublishAsync(PublishMessageRequest request)
    {
        var subscriber = _connectionFactory.GetSubscriber();
        var channel = string.IsNullOrWhiteSpace(request.Channel)
            ? NotificationsChannel
            : request.Channel.Trim();
        var message = string.IsNullOrWhiteSpace(request.Message)
            ? $"Demo notification {DateTimeOffset.Now:HH:mm:ss}"
            : request.Message.Trim();

        return await subscriber.PublishAsync(RedisChannel.Literal(channel), message);
    }

    public async Task<RedisOverviewResponse> GetOverviewAsync()
    {
        var db = _connectionFactory.GetDatabase();
        var server = _connectionFactory.GetServer();
        var info = await server.InfoAsync();

        return new RedisOverviewResponse
        {
            Online = true,
            Endpoint = _connectionFactory.GetCurrentRedisEndpointText(),
            TotalKeys = await CountKeysAsync(server),
            UsedMemoryHuman = ReadInfo(info, "Memory", "used_memory_human"),
            ConnectedClients = int.TryParse(ReadInfo(info, "Clients", "connected_clients"), out var clients) ? clients : 0,
            Uptime = TimeSpan.FromSeconds(long.TryParse(ReadInfo(info, "Server", "uptime_in_seconds"), out var uptime) ? uptime : 0),
            Cache = await GetCacheMonitorAsync(),
            ActiveSessions = await CountPatternAsync(server, "session:*"),
            ActiveCarts = await CountPatternAsync(server, "cart:*"),
            RateLimitedClients = await CountPatternAsync(server, "rate:*"),
            StreamLength = await db.StreamLengthAsync(OrdersStreamKey)
        };
    }

    public async Task<RedisDashboardDetailsResponse> GetDashboardDetailsAsync()
    {
        var db = _connectionFactory.GetDatabase();
        var server = _connectionFactory.GetServer();

        return new RedisDashboardDetailsResponse
        {
            Sessions = await GetSessionMonitorsAsync(db, server),
            Carts = await GetCartMonitorsAsync(db, server),
            RateLimits = await GetRateLimitMonitorsAsync(db, server),
            Stream = await GetStreamMonitorAsync(db)
        };
    }

    public async Task<RedisInfrastructureResponse> GetInfrastructureAsync()
    {
        var server = _connectionFactory.GetServer();
        var info = await server.InfoAsync();
        var db = _connectionFactory.GetDatabase();
        var cacheDb = _connectionFactory.GetCacheDatabase();
        var persistenceDb = _connectionFactory.GetPersistenceDatabase();

        return new RedisInfrastructureResponse
        {
            Endpoint = _connectionFactory.GetCurrentRedisEndpointText(),
            ConnectionMode = _connectionFactory.GetConfiguredMode(),
            ConfiguredEndpoint = _connectionFactory.GetConfiguredEndpoint(),
            Nodes = await BuildNodeStatusesAsync(_connectionFactory.GetCurrentRedisEndpointText()),
            Role = ReadInfo(info, "Replication", "role"),
            RedisVersion = ReadInfo(info, "Server", "redis_version"),
            Os = ReadInfo(info, "Server", "os"),
            Mode = ReadInfo(info, "Server", "redis_mode"),
            PersistenceRdb = ReadInfo(info, "Persistence", "rdb_last_bgsave_status"),
            PersistenceAof = ReadInfo(info, "Persistence", "aof_enabled"),
            ConnectedReplicas = ReadInfo(info, "Replication", "connected_slaves"),
            MasterLinkStatus = ReadInfo(info, "Replication", "master_link_status"),
            UsedMemoryPeakHuman = ReadInfo(info, "Memory", "used_memory_peak_human"),
            MaxMemoryHuman = ReadInfo(info, "Memory", "maxmemory_human"),
            MaxMemoryPolicy = await ReadConfigValueAsync(db, "maxmemory-policy"),
            CacheEndpoint = _connectionFactory.GetCacheEndpointText(),
            CacheMaxMemory = await ReadConfigValueAsync(cacheDb, "maxmemory"),
            CacheMaxMemoryPolicy = await ReadConfigValueAsync(cacheDb, "maxmemory-policy"),
            PersistenceEndpoint = _connectionFactory.GetPersistenceEndpointText(),
            PersistenceAppendOnly = await ReadConfigValueAsync(persistenceDb, "appendonly"),
            PersistenceSave = await ReadConfigValueAsync(persistenceDb, "save")
        };
    }

    private async Task<List<RedisNodeStatusDto>> BuildNodeStatusesAsync(string currentMaster)
    {
        var nodes = _options.Sentinel.Endpoints.Count > 0
            ? _options.Sentinel.Endpoints
                .Select((endpoint, index) => new
                {
                    Name = $"redis-node-{index + 1}",
                    RedisEndpoint = $"{endpoint.Host}:6379",
                    SentinelEndpoint = $"{endpoint.Host}:{endpoint.Port}"
                })
                .ToList()
            : [new { Name = "redis-node-1", RedisEndpoint = $"{_options.Host}:{_options.Port}", SentinelEndpoint = "" }];

        var result = new List<RedisNodeStatusDto>();
        foreach (var node in nodes)
        {
            result.Add(await ReadNodeStatusAsync(node.Name, node.RedisEndpoint, node.SentinelEndpoint, currentMaster));
        }

        return result;
    }

    private async Task<RedisNodeStatusDto> ReadNodeStatusAsync(string name, string redisEndpoint, string sentinelEndpoint, string currentMaster)
    {
        try
        {
            var parts = redisEndpoint.Split(':', StringSplitOptions.RemoveEmptyEntries);
            var host = parts[0];
            var port = parts.Length > 1 && int.TryParse(parts[1], out var parsedPort) ? parsedPort : 6379;
            var configuration = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
                AllowAdmin = true,
                ConnectRetry = 1,
                ConnectTimeout = 1500,
                SyncTimeout = 1500
            };

            configuration.EndPoints.Add(host, port);
            if (!string.IsNullOrWhiteSpace(_options.Password))
            {
                configuration.Password = _options.Password;
            }

            await using var connection = await ConnectionMultiplexer.ConnectAsync(configuration);
            var server = connection.GetServer(host, port);
            var info = await server.InfoAsync();
            var db = connection.GetDatabase();
            var role = ReadInfo(info, "Replication", "role");
            var masterLinkStatus = ReadInfo(info, "Replication", "master_link_status");
            var connectedSlaves = ReadInfo(info, "Replication", "connected_slaves");
            var masterHost = ReadInfo(info, "Replication", "master_host");
            var isCurrentMaster = string.Equals(redisEndpoint, currentMaster, StringComparison.OrdinalIgnoreCase);
            var roleLabel = string.Equals(role, "master", StringComparison.OrdinalIgnoreCase) ? "Master" : "Replica";
            var status = roleLabel == "Master" || string.Equals(masterLinkStatus, "up", StringComparison.OrdinalIgnoreCase)
                ? "Online"
                : "Degraded";
            var note = roleLabel == "Master"
                ? $"Master hiện tại, {connectedSlaves} replica đang kết nối"
                : string.Equals(masterLinkStatus, "up", StringComparison.OrdinalIgnoreCase)
                    ? $"Đang đồng bộ từ {masterHost}:6379"
                    : $"Replica chưa đồng bộ, master_link_status={masterLinkStatus}";

            return new RedisNodeStatusDto
            {
                Name = name,
                Endpoint = redisEndpoint,
                SentinelEndpoint = sentinelEndpoint,
                Role = roleLabel,
                Status = isCurrentMaster ? "Current master" : status,
                Note = note,
                RedisVersion = ReadInfo(info, "Server", "redis_version"),
                Os = ReadInfo(info, "Server", "os"),
                Mode = ReadInfo(info, "Server", "redis_mode"),
                PersistenceRdb = ReadInfo(info, "Persistence", "rdb_last_bgsave_status"),
                PersistenceAof = ReadInfo(info, "Persistence", "aof_enabled"),
                ConnectedReplicas = connectedSlaves,
                MasterLinkStatus = masterLinkStatus,
                UsedMemoryPeakHuman = ReadInfo(info, "Memory", "used_memory_peak_human"),
                MaxMemoryPolicy = await ReadConfigValueAsync(db, "maxmemory-policy"),
                MaxMemoryHuman = ReadInfo(info, "Memory", "maxmemory_human")
            };
        }
        catch (RedisException exception)
        {
            return new RedisNodeStatusDto
            {
                Name = name,
                Endpoint = redisEndpoint,
                SentinelEndpoint = sentinelEndpoint,
                Role = "Unknown",
                Status = "Offline",
                Note = exception.Message
            };
        }
    }

    public async Task<RedisPersistenceTestResponse> PreparePersistenceTestAsync()
    {
        var db = _connectionFactory.GetPersistenceDatabase();
        var now = DateTimeOffset.Now.ToString("O");

        await db.StringSetAsync("persistence:test:cache", JsonSerializer.Serialize(new
        {
            name = "cache test",
            createdAt = now
        }));

        await db.HashSetAsync("persistence:test:session",
        [
            new HashEntry("userId", 1),
            new HashEntry("username", "persistence_user"),
            new HashEntry("createdAt", now)
        ]);

        await db.HashSetAsync("persistence:test:cart",
        [
            new HashEntry("1", 2),
            new HashEntry("2", 1)
        ]);

        await db.SortedSetAddAsync("persistence:test:ranking",
        [
            new SortedSetEntry("product:1", 10),
            new SortedSetEntry("product:2", 7)
        ]);

        await db.StreamAddAsync("persistence:test:stream",
        [
            new NameValueEntry("event", "persistence-test"),
            new NameValueEntry("createdAt", now)
        ]);

        await TryBackgroundSaveAsync(db);

        return await BuildPersistenceTestResponseAsync(
            "Prepared",
            "Da tao 5 key test tren Redis 6381. Hay chay tren Ubuntu: sudo systemctl restart redis-persistence-6381, sau do quay lai bam Kiem tra sau restart.");
    }

    public async Task<RedisPersistenceTestResponse> CheckPersistenceTestAsync()
    {
        return await BuildPersistenceTestResponseAsync(
            "Checked",
            "PASS neu ca 5 key test van ton tai sau khi restart Redis.");
    }

    public async Task ClearPersistenceTestAsync()
    {
        var db = _connectionFactory.GetPersistenceDatabase();
        await db.KeyDeleteAsync(PersistenceTestKeys);
    }

    private async Task<CacheMonitorDto> GetCacheMonitorAsync()
    {
        var db = _connectionFactory.GetCacheDatabase();
        var ttl = await db.KeyTimeToLiveAsync(ProductsCacheKey);
        var redisDuration = ToLong(await db.StringGetAsync(CacheRedisDurationKey));
        var mySqlDuration = ToLong(await db.StringGetAsync(CacheMySqlDurationKey));
        return new CacheMonitorDto
        {
            LastSource = (await db.StringGetAsync(CacheSourceKey)).ToString(),
            Hit = ToLong(await db.StringGetAsync(CacheHitKey).ConfigureAwait(false)),
            Miss = ToLong(await db.StringGetAsync(CacheMissKey).ConfigureAwait(false)),
            Ttl = (long)Math.Max(0, ttl?.TotalSeconds ?? 0),
            LastDurationMs = ToLong(await db.StringGetAsync(CacheLastDurationKey)),
            RedisDurationMs = redisDuration,
            MySqlDurationMs = mySqlDuration,
            Speedup = redisDuration > 0 && mySqlDuration > 0
                ? Math.Round((double)mySqlDuration / redisDuration, 1)
                : 0
        };
    }

    private async Task<RedisPersistenceTestResponse> BuildPersistenceTestResponseAsync(string status, string instruction)
    {
        var db = _connectionFactory.GetPersistenceDatabase();
        var server = _connectionFactory.GetPersistenceServer();
        var keys = new List<PersistenceTestKeyDto>();

        foreach (var key in PersistenceTestKeys)
        {
            keys.Add(await BuildPersistenceTestKeyAsync(db, key));
        }

        var existingCount = keys.Count(item => item.Exists);

        return new RedisPersistenceTestResponse
        {
            Status = status,
            TotalKeys = await CountKeysAsync(server),
            ExistingCount = existingCount,
            MissingCount = keys.Count - existingCount,
            Pass = existingCount == keys.Count,
            Instruction = instruction,
            CheckedAt = DateTimeOffset.Now,
            Keys = keys
        };
    }

    private static async Task<PersistenceTestKeyDto> BuildPersistenceTestKeyAsync(IDatabase db, RedisKey key)
    {
        var type = await db.KeyTypeAsync(key);
        var exists = type != RedisType.None;
        var ttl = await db.KeyTimeToLiveAsync(key);

        return new PersistenceTestKeyDto
        {
            Key = key.ToString(),
            Type = type.ToString(),
            Exists = exists,
            Ttl = exists ? (long)(ttl?.TotalSeconds ?? -1) : -2,
            ValuePreview = exists ? await BuildValuePreviewAsync(db, key, type) : "Missing"
        };
    }

    private static async Task<string> BuildValuePreviewAsync(IDatabase db, RedisKey key, RedisType type)
    {
        return type switch
        {
            RedisType.String => Truncate((await db.StringGetAsync(key)).ToString(), 80),
            RedisType.Hash => $"Hash fields: {await db.HashLengthAsync(key)}",
            RedisType.SortedSet => $"Sorted set members: {await db.SortedSetLengthAsync(key)}",
            RedisType.Stream => $"Stream messages: {await db.StreamLengthAsync(key)}",
            RedisType.List => $"List items: {await db.ListLengthAsync(key)}",
            RedisType.Set => $"Set members: {await db.SetLengthAsync(key)}",
            _ => type.ToString()
        };
    }

    private static async Task TryBackgroundSaveAsync(IDatabase db)
    {
        try
        {
            await db.ExecuteAsync("BGSAVE");
        }
        catch (RedisServerException)
        {
            // Redis may already be saving in the background. The test data is still written.
        }
    }

    private async Task<List<SessionMonitorDto>> GetSessionMonitorsAsync(IDatabase db, IServer server)
    {
        var sessions = new List<SessionMonitorDto>();
        await foreach (var key in server.KeysAsync(pattern: "session:*"))
        {
            var entries = await db.HashGetAllAsync(key);
            var byName = entries.ToDictionary(entry => entry.Name.ToString(), entry => entry.Value.ToString());
            var ttl = await db.KeyTimeToLiveAsync(key);

            sessions.Add(new SessionMonitorDto
            {
                Key = key.ToString(),
                UserId = long.TryParse(byName.GetValueOrDefault("userId"), out var userId) ? userId : 0,
                Username = byName.GetValueOrDefault("username") ?? "",
                Role = byName.GetValueOrDefault("role") ?? "",
                CreatedAt = byName.GetValueOrDefault("createdAt") ?? "",
                Ttl = (long)Math.Max(0, ttl?.TotalSeconds ?? 0)
            });
        }

        return sessions.OrderBy(item => item.Username).ToList();
    }

    private async Task<List<CartMonitorDto>> GetCartMonitorsAsync(IDatabase db, IServer server)
    {
        var carts = new List<CartMonitorDto>();
        var products = await _productRepository.GetProductsAsync();
        var productsById = products.ToDictionary(product => product.Id);

        await foreach (var key in server.KeysAsync(pattern: "cart:*"))
        {
            var entries = await db.HashGetAllAsync(key);
            var items = entries.Select(entry =>
            {
                var productId = long.TryParse(entry.Name.ToString(), out var parsedProductId) ? parsedProductId : 0;
                var quantity = int.TryParse(entry.Value.ToString(), out var parsedQuantity) ? parsedQuantity : 0;
                productsById.TryGetValue(productId, out var product);
                var price = product?.Price ?? 0;

                return new CartItemDto
                {
                    ProductId = productId,
                    Name = product?.Name ?? $"Product {productId}",
                    Price = price,
                    Quantity = quantity,
                    Subtotal = price * quantity
                };
            }).ToList();
            var ttl = await db.KeyTimeToLiveAsync(key);
            var keyText = key.ToString();

            carts.Add(new CartMonitorDto
            {
                Key = keyText,
                UserId = long.TryParse(keyText.Replace("cart:", ""), out var userId) ? userId : 0,
                ItemCount = items.Count,
                TotalQuantity = items.Sum(item => item.Quantity),
                TotalAmount = items.Sum(item => item.Subtotal),
                Ttl = (long)Math.Max(0, ttl?.TotalSeconds ?? 0),
                Items = items
            });
        }

        return carts.OrderBy(item => item.UserId).ToList();
    }

    private async Task<List<RateLimitMonitorDto>> GetRateLimitMonitorsAsync(IDatabase db, IServer server)
    {
        var rates = new List<RateLimitMonitorDto>();
        await foreach (var key in server.KeysAsync(pattern: "rate:*"))
        {
            var count = ToLong(await db.StringGetAsync(key));
            var ttl = await db.KeyTimeToLiveAsync(key);
            var keyText = key.ToString();

            rates.Add(new RateLimitMonitorDto
            {
                Key = keyText,
                Client = keyText.Replace("rate:", ""),
                Count = count,
                Limit = _options.RateLimitMaxRequests,
                Ttl = (long)Math.Max(0, ttl?.TotalSeconds ?? 0),
                Blocked = count > _options.RateLimitMaxRequests
            });
        }

        return rates.OrderByDescending(item => item.Count).ToList();
    }

    private async Task<StreamMonitorDto> GetStreamMonitorAsync(IDatabase db)
    {
        var pendingResult = await ExecuteRedisResultAsync(db, "XPENDING", OrdersStreamKey, OrdersConsumerGroup);
        var groupsResult = await ExecuteRedisResultAsync(db, "XINFO", "GROUPS", OrdersStreamKey);
        var pendingCount = ReadPendingCount(pendingResult);
        var groupInfo = ReadGroupInfo(groupsResult);

        return new StreamMonitorDto
        {
            Key = OrdersStreamKey,
            ConsumerGroup = OrdersConsumerGroup,
            Length = await db.StreamLengthAsync(OrdersStreamKey),
            PendingCount = pendingCount,
            ConsumerCount = groupInfo.ConsumerCount,
            LastDeliveredId = groupInfo.LastDeliveredId,
            WorkerStatus = pendingCount == 0 ? "Running" : "Pending",
            PendingSummary = FormatPendingSummary(pendingResult),
            GroupsSummary = FormatGroupSummary(groupInfo)
        };
    }

    private static async Task<RedisResult> ExecuteRedisResultAsync(IDatabase db, params object[] args)
    {
        try
        {
            return await db.ExecuteAsync(args[0].ToString()!, args.Skip(1).ToArray());
        }
        catch (RedisServerException exception)
        {
            return RedisResult.Create((RedisValue)exception.Message);
        }
    }

    private static async Task<string> ReadConfigValueAsync(IDatabase db, string name)
    {
        try
        {
            var result = await db.ExecuteAsync("CONFIG", "GET", name);
            var items = ToRedisResultArray(result);
            return items.Length >= 2 ? items[1].ToString() : "";
        }
        catch (RedisException exception)
        {
            return exception.Message;
        }
    }

    private static long ReadPendingCount(RedisResult result)
    {
        var items = ToRedisResultArray(result);
        if (items.Length == 0)
        {
            return 0;
        }

        return long.TryParse(items[0].ToString(), out var count) ? count : 0;
    }

    private static (int ConsumerCount, string LastDeliveredId) ReadGroupInfo(RedisResult result)
    {
        var groups = ToRedisResultArray(result);
        if (groups.Length == 0)
        {
            return (0, "");
        }

        var firstGroup = ToRedisResultArray(groups[0]);
        var consumerCount = 0;
        var lastDeliveredId = "";

        for (var index = 0; index < firstGroup.Length - 1; index += 2)
        {
            var key = firstGroup[index].ToString();
            var value = firstGroup[index + 1].ToString();

            if (string.Equals(key, "consumers", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(value, out consumerCount);
            }

            if (string.Equals(key, "last-delivered-id", StringComparison.OrdinalIgnoreCase))
            {
                lastDeliveredId = value;
            }
        }

        return (consumerCount, lastDeliveredId);
    }

    private static string FormatPendingSummary(RedisResult result)
    {
        var items = ToRedisResultArray(result);
        if (items.Length == 0)
        {
            return result.ToString();
        }

        var count = items.Length > 0 ? items[0].ToString() : "0";
        var minId = items.Length > 1 ? items[1].ToString() : "";
        var maxId = items.Length > 2 ? items[2].ToString() : "";

        return string.IsNullOrWhiteSpace(minId) || minId == "(nil)"
            ? $"Pending: {count}"
            : $"Pending: {count}, range: {minId} -> {maxId}";
    }

    private static string FormatGroupSummary((int ConsumerCount, string LastDeliveredId) groupInfo)
    {
        return $"Consumers: {groupInfo.ConsumerCount}, last delivered: {groupInfo.LastDeliveredId}";
    }

    private static RedisResult[] ToRedisResultArray(RedisResult result)
    {
        if (result.IsNull)
        {
            return [];
        }

        try
        {
#pragma warning disable CS8600
            RedisResult[]? items = (RedisResult[])result;
#pragma warning restore CS8600
            return items ?? [];
        }
        catch (InvalidCastException)
        {
            return [];
        }
    }

    private static string GetCartKey(long userId) => $"cart:{userId}";

    private static string GetProductDetailCacheKey(long productId) => $"cache:product:{productId}";

    private static string GetProductDetailRebuildLockKey(long productId) => $"lock:cache:product:{productId}:rebuild";

    private static long ToLong(RedisValue value)
    {
        return value.HasValue && long.TryParse(value.ToString(), out var number) ? number : 0;
    }

    private static string GetStreamValue(StreamEntry entry, string name)
    {
        return entry.Values.FirstOrDefault(value => value.Name == name).Value.ToString();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...";
    }

    private static string ReadInfo(IGrouping<string, KeyValuePair<string, string>>[] info, string group, string key)
    {
        return info
            .FirstOrDefault(item => string.Equals(item.Key, group, StringComparison.OrdinalIgnoreCase))
            ?.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
            .Value ?? "";
    }

    private static async Task<long> CountKeysAsync(IServer server)
    {
        long count = 0;
        await foreach (var key in server.KeysAsync(pattern: "*"))
        {
            count++;
        }
        return count;
    }

    private static async Task<int> CountPatternAsync(IServer server, string pattern)
    {
        var count = 0;
        await foreach (var key in server.KeysAsync(pattern: pattern))
        {
            count++;
        }
        return count;
    }
}

using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;
using StackExchange.Redis;

namespace RedisDemo.Api.Data;

public sealed class RedisConnectionFactory : IAsyncDisposable
{
    private readonly object _lock = new();
    private readonly RedisOptions _options;
    private ConnectionMultiplexer? _connection;
    private ConnectionMultiplexer? _sentinelConnection;
    private ConnectionMultiplexer? _cacheConnection;
    private ConnectionMultiplexer? _persistenceConnection;
    private RedisEndpointOptions? _connectedEndpoint;
    private RedisEndpointOptions? _cachedMasterEndpoint;
    private DateTimeOffset _cachedMasterEndpointExpiresAt = DateTimeOffset.MinValue;
    private DateTimeOffset _cacheRedisDisabledUntil = DateTimeOffset.MinValue;

    public RedisConnectionFactory(IOptions<RedisOptions> options)
    {
        _options = options.Value;
    }

    public IDatabase GetDatabase()
    {
        return GetConnection().GetDatabase(_options.Database);
    }

    public IDatabase GetCacheDatabase()
    {
        return GetCacheConnection().GetDatabase(GetDirectDatabase(_options.Cache));
    }

    public IDatabase GetPersistenceDatabase()
    {
        return GetPersistenceConnection().GetDatabase(GetDirectDatabase(_options.Persistence));
    }

    public ISubscriber GetSubscriber()
    {
        return GetConnection().GetSubscriber();
    }

    public IServer GetServer()
    {
        var endpoint = GetCurrentRedisEndpoint();
        return GetConnection().GetServer(endpoint.Host, endpoint.Port);
    }

    public IServer GetCacheServer()
    {
        var endpoint = GetDirectEndpoint(_options.Cache, _options.Host, _options.Port);
        return GetCacheConnection().GetServer(endpoint.Host, endpoint.Port);
    }

    public IServer GetPersistenceServer()
    {
        var endpoint = GetDirectEndpoint(_options.Persistence, _options.Host, _options.Port);
        return GetPersistenceConnection().GetServer(endpoint.Host, endpoint.Port);
    }

    public bool ShouldSkipOptionalRedis()
    {
        return DateTimeOffset.UtcNow < _cacheRedisDisabledUntil;
    }

    public void MarkOptionalRedisFailure(TimeSpan duration)
    {
        var disabledUntil = DateTimeOffset.UtcNow.Add(duration);
        if (disabledUntil > _cacheRedisDisabledUntil)
        {
            _cacheRedisDisabledUntil = disabledUntil;
        }
    }

    public void MarkOptionalRedisSuccess()
    {
        _cacheRedisDisabledUntil = DateTimeOffset.MinValue;
    }

    public async Task<bool> CanReachAnyConfiguredEndpointAsync(TimeSpan timeout)
    {
        var endpoints = IsSentinelEnabled()
            ? _options.Sentinel.Endpoints
                .Concat([new RedisEndpointOptions { Host = _options.Host, Port = _options.Port }])
                .ToList()
            : [new RedisEndpointOptions { Host = _options.Host, Port = _options.Port }];

        var checks = endpoints
            .Select(endpoint => CanOpenTcpConnectionAsync(endpoint.Host, endpoint.Port, timeout))
            .ToArray();
        var results = await Task.WhenAll(checks);
        return results.Any(result => result);
    }

    public RedisEndpointOptions GetCurrentRedisEndpoint()
    {
        if (IsSentinelEnabled())
        {
            return GetMasterEndpointFromSentinel();
        }

        return new RedisEndpointOptions
        {
            Host = _options.Host,
            Port = _options.Port
        };
    }

    public string GetCurrentRedisEndpointText()
    {
        var endpoint = GetCurrentRedisEndpoint();
        return $"{endpoint.Host}:{endpoint.Port}";
    }

    public string GetConfiguredMode()
    {
        return IsSentinelEnabled() ? "Sentinel" : "Direct";
    }

    public string GetConfiguredEndpoint()
    {
        if (!IsSentinelEnabled())
        {
            return $"{_options.Host}:{_options.Port}";
        }

        return $"{_options.Sentinel.ServiceName} via {string.Join(", ", _options.Sentinel.Endpoints.Select(item => $"{item.Host}:{item.Port}"))}";
    }

    public string GetCacheEndpointText()
    {
        var endpoint = GetDirectEndpoint(_options.Cache, _options.Host, _options.Port);
        return $"{endpoint.Host}:{endpoint.Port}";
    }

    public string GetPersistenceEndpointText()
    {
        var endpoint = GetDirectEndpoint(_options.Persistence, _options.Host, _options.Port);
        return $"{endpoint.Host}:{endpoint.Port}";
    }

    public List<string> GetReplicaEndpointTexts()
    {
        if (!IsSentinelEnabled())
        {
            return [];
        }

        foreach (var sentinelEndpoint in _options.Sentinel.Endpoints)
        {
            try
            {
                using var sentinel = CreateSentinelConnection(sentinelEndpoint);
                var server = sentinel.GetServer(sentinelEndpoint.Host, sentinelEndpoint.Port);
                var replicas = server.SentinelGetReplicaAddresses(_options.Sentinel.ServiceName);
                return replicas
                    .Select(ParseEndpoint)
                    .Where(endpoint => endpoint is not null)
                    .Select(endpoint => $"{endpoint!.Host}:{endpoint.Port}")
                    .ToList();
            }
            catch (RedisException)
            {
                // Try the next Sentinel endpoint.
            }
        }

        return [];
    }

    private ConnectionMultiplexer GetConnection()
    {
        lock (_lock)
        {
            var expectedEndpoint = GetCurrentRedisEndpoint();
            if (_connection is not null
                && _connection.IsConnected
                && _connectedEndpoint is not null
                && EndpointEquals(_connectedEndpoint, expectedEndpoint))
            {
                return _connection;
            }

            var oldConnection = _connection;
            try
            {
                _connection = CreateConnection(expectedEndpoint);
                _connectedEndpoint = expectedEndpoint;
                oldConnection?.Dispose();
            }
            catch (RedisException)
            {
                _cachedMasterEndpoint = null;
                _cachedMasterEndpointExpiresAt = DateTimeOffset.MinValue;
                MarkOptionalRedisFailure(TimeSpan.FromSeconds(20));
                throw;
            }

            return _connection;
        }
    }

    private ConnectionMultiplexer GetSentinelConnection()
    {
        if (_sentinelConnection is not null && _sentinelConnection.IsConnected)
        {
            return _sentinelConnection;
        }

        _sentinelConnection?.Dispose();
        _sentinelConnection = CreateSentinelConnection();
        return _sentinelConnection;
    }

    private ConnectionMultiplexer GetCacheConnection()
    {
        var endpoint = GetDirectEndpoint(_options.Cache, _options.Host, _options.Port);
        _cacheConnection = GetOrCreateDirectConnection(_cacheConnection, endpoint, _options.Cache);
        return _cacheConnection;
    }

    private ConnectionMultiplexer GetPersistenceConnection()
    {
        var endpoint = GetDirectEndpoint(_options.Persistence, _options.Host, _options.Port);
        _persistenceConnection = GetOrCreateDirectConnection(_persistenceConnection, endpoint, _options.Persistence);
        return _persistenceConnection;
    }

    private ConnectionMultiplexer GetOrCreateDirectConnection(
        ConnectionMultiplexer? existingConnection,
        RedisEndpointOptions endpoint,
        RedisDirectOptions directOptions)
    {
        if (existingConnection is not null && existingConnection.IsConnected)
        {
            return existingConnection;
        }

        existingConnection?.Dispose();
        return CreateDirectConnection(endpoint, directOptions);
    }

    private RedisEndpointOptions GetMasterEndpointFromSentinel()
    {
        if (_cachedMasterEndpoint is not null && DateTimeOffset.UtcNow < _cachedMasterEndpointExpiresAt)
        {
            return _cachedMasterEndpoint;
        }

        foreach (var sentinelEndpoint in _options.Sentinel.Endpoints)
        {
            try
            {
                using var sentinel = CreateSentinelConnection(sentinelEndpoint);
                var server = sentinel.GetServer(sentinelEndpoint.Host, sentinelEndpoint.Port);
                var masterEndpoint = server.SentinelGetMasterAddressByName(_options.Sentinel.ServiceName);
                var endpoint = ParseEndpoint(masterEndpoint);
                if (endpoint is not null)
                {
                    _cachedMasterEndpoint = endpoint;
                    _cachedMasterEndpointExpiresAt = DateTimeOffset.UtcNow.AddSeconds(3);
                    return endpoint;
                }
            }
            catch (RedisException)
            {
                // Try the next Sentinel endpoint.
            }
        }

        return new RedisEndpointOptions
        {
            Host = _options.Host,
            Port = _options.Port
        };
    }

    private static RedisEndpointOptions? ParseEndpoint(EndPoint? endpoint)
    {
        if (endpoint is null)
        {
            return null;
        }

        if (endpoint is IPEndPoint ipEndpoint)
        {
            return new RedisEndpointOptions
            {
                Host = ipEndpoint.Address.ToString(),
                Port = ipEndpoint.Port
            };
        }

        if (endpoint is DnsEndPoint dnsEndpoint)
        {
            return new RedisEndpointOptions
            {
                Host = dnsEndpoint.Host,
                Port = dnsEndpoint.Port
            };
        }

        var text = endpoint.ToString() ?? "";
        var parts = text.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && int.TryParse(parts[^1], out var port))
        {
            return new RedisEndpointOptions
            {
                Host = string.Join(":", parts[..^1]),
                Port = port
            };
        }

        return null;
    }

    private ConnectionMultiplexer CreateConnection(RedisEndpointOptions endpoint)
    {
        var configuration = CreateBaseConfiguration();
        configuration.EndPoints.Add(endpoint.Host, endpoint.Port);

        return ConnectionMultiplexer.Connect(configuration);
    }

    private ConnectionMultiplexer CreateDirectConnection(RedisEndpointOptions endpoint, RedisDirectOptions directOptions)
    {
        var configuration = CreateBaseConfiguration(directOptions);
        configuration.EndPoints.Add(endpoint.Host, endpoint.Port);

        return ConnectionMultiplexer.Connect(configuration);
    }

    private ConnectionMultiplexer CreateSentinelConnection()
    {
        var configuration = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            AllowAdmin = true,
            ConnectRetry = 1,
            ConnectTimeout = 500,
            SyncTimeout = 500,
            TieBreaker = ""
        };

        foreach (var endpoint in _options.Sentinel.Endpoints)
        {
            configuration.EndPoints.Add(endpoint.Host, endpoint.Port);
        }

        return ConnectionMultiplexer.SentinelConnect(configuration);
    }

    private static ConnectionMultiplexer CreateSentinelConnection(RedisEndpointOptions endpoint)
    {
        var configuration = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            AllowAdmin = true,
            ConnectRetry = 1,
            ConnectTimeout = 500,
            SyncTimeout = 500,
            TieBreaker = ""
        };

        configuration.EndPoints.Add(endpoint.Host, endpoint.Port);
        return ConnectionMultiplexer.SentinelConnect(configuration);
    }

    private ConfigurationOptions CreateBaseConfiguration()
    {
        return CreateBaseConfiguration(null);
    }

    private ConfigurationOptions CreateBaseConfiguration(RedisDirectOptions? directOptions)
    {
        var configuration = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            AllowAdmin = true,
            ConnectRetry = 1,
            ConnectTimeout = 500,
            SyncTimeout = 500,
            DefaultDatabase = directOptions?.Database ?? _options.Database
        };

        var password = directOptions is not null && !string.IsNullOrWhiteSpace(directOptions.Password)
            ? directOptions.Password
            : _options.Password;

        if (!string.IsNullOrWhiteSpace(password))
        {
            configuration.Password = password;
        }

        return configuration;
    }

    private static RedisEndpointOptions GetDirectEndpoint(RedisDirectOptions options, string fallbackHost, int fallbackPort)
    {
        if (options.Enabled && !string.IsNullOrWhiteSpace(options.Host) && options.Port > 0)
        {
            return new RedisEndpointOptions
            {
                Host = options.Host,
                Port = options.Port
            };
        }

        return new RedisEndpointOptions
        {
            Host = fallbackHost,
            Port = fallbackPort
        };
    }

    private static int GetDirectDatabase(RedisDirectOptions options)
    {
        return options.Enabled ? options.Database : 0;
    }

    private bool IsSentinelEnabled()
    {
        return _options.Sentinel.Enabled
            && !string.IsNullOrWhiteSpace(_options.Sentinel.ServiceName)
            && _options.Sentinel.Endpoints.Count > 0;
    }

    private static bool EndpointEquals(RedisEndpointOptions first, RedisEndpointOptions second)
    {
        return string.Equals(first.Host, second.Host, StringComparison.OrdinalIgnoreCase)
            && first.Port == second.Port;
    }

    private static async Task<bool> CanOpenTcpConnectionAsync(string host, int port, TimeSpan timeout)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port).WaitAsync(timeout);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        if (_sentinelConnection is not null)
        {
            await _sentinelConnection.DisposeAsync();
        }

        if (_cacheConnection is not null)
        {
            await _cacheConnection.DisposeAsync();
        }

        if (_persistenceConnection is not null)
        {
            await _persistenceConnection.DisposeAsync();
        }
    }
}

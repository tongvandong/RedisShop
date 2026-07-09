using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using RedisDemo.Api.Data;
using StackExchange.Redis;

namespace RedisDemo.Api.Services;

public sealed class RedisPubSubBroker
{
    private const string NotificationsChannel = "notifications";
    private readonly ConcurrentDictionary<Guid, Channel<string>> _clients = new();
    private readonly RedisConnectionFactory _connectionFactory;
    private readonly SemaphoreSlim _subscribeLock = new(1, 1);
    private bool _isSubscribed;

    public RedisPubSubBroker(RedisConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public int ConnectedClients => _clients.Count;

    public async Task<ChannelReader<string>> RegisterClientAsync(CancellationToken cancellationToken)
    {
        await EnsureSubscribedAsync();

        var clientId = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<string>();
        _clients[clientId] = channel;

        cancellationToken.Register(() => UnregisterClient(clientId));
        return channel.Reader;
    }

    private async Task EnsureSubscribedAsync()
    {
        if (_isSubscribed)
        {
            return;
        }

        await _subscribeLock.WaitAsync();
        try
        {
            if (_isSubscribed)
            {
                return;
            }

            var subscriber = _connectionFactory.GetSubscriber();
            await subscriber.SubscribeAsync(RedisChannel.Literal(NotificationsChannel), (_, message) =>
            {
                Broadcast(BuildNotificationPayload(message.ToString()));
            });
            _isSubscribed = true;
        }
        finally
        {
            _subscribeLock.Release();
        }
    }

    private void Broadcast(string payload)
    {
        foreach (var client in _clients)
        {
            if (!client.Value.Writer.TryWrite(payload))
            {
                UnregisterClient(client.Key);
            }
        }
    }

    private void UnregisterClient(Guid clientId)
    {
        if (_clients.TryRemove(clientId, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }

    private static string BuildNotificationPayload(string message)
    {
        return JsonSerializer.Serialize(new
        {
            channel = NotificationsChannel,
            message,
            time = DateTimeOffset.Now.ToString("HH:mm:ss"),
            receivedBy = "ASP.NET Core Subscriber"
        });
    }
}

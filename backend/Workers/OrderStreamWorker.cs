using RedisDemo.Api.Repositories;

namespace RedisDemo.Api.Workers;

public sealed class OrderStreamWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderStreamWorker> _logger;
    private readonly string _consumerName = $"worker-{Environment.MachineName}";

    public OrderStreamWorker(IServiceScopeFactory scopeFactory, ILogger<OrderStreamWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var redisRepository = scope.ServiceProvider.GetRequiredService<RedisRepository>();
                var orderRepository = scope.ServiceProvider.GetRequiredService<OrderRepository>();

                await redisRepository.EnsureOrderConsumerGroupAsync();
                var staleMessages = await redisRepository.ClaimStaleOrderStreamMessagesAsync(_consumerName);
                await ProcessMessagesAsync(staleMessages, orderRepository, redisRepository);

                var messages = await redisRepository.ReadOrderStreamAsync(_consumerName);
                await ProcessMessagesAsync(messages, orderRepository, redisRepository);
            }
            catch (Exception exception)
            {
                _logger.LogWarning("Order stream worker paused: {Message}", exception.Message);
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    private async Task ProcessMessagesAsync(
        IEnumerable<StackExchange.Redis.StreamEntry> messages,
        OrderRepository orderRepository,
        RedisRepository redisRepository)
    {
        foreach (var message in messages)
        {
            var orderId = redisRepository.ReadOrderId(message);
            if (orderId > 0)
            {
                var processedNow = await orderRepository.MarkOrderProcessedAsync(orderId);
                if (processedNow)
                {
                    var items = await orderRepository.GetOrderItemsForRankingAsync(orderId);
                    await redisRepository.IncrementProductRankingForOrderAsync(orderId, items);
                }
            }

            await redisRepository.AckOrderStreamAsync(message.Id);
            _logger.LogInformation("Processed order stream message {MessageId} for order {OrderId}", message.Id, orderId);
        }
    }
}

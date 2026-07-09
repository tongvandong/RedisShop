using Microsoft.AspNetCore.Mvc;
using RedisDemo.Api.Dtos;
using RedisDemo.Api.Repositories;
using RedisDemo.Api.Services;

namespace RedisDemo.Api.Controllers;

[ApiController]
[Route("api/redis")]
public sealed class RedisController : ControllerBase
{
    private readonly RedisRepository _redisRepository;
    private readonly ProductRepository _productRepository;
    private readonly RedisPubSubBroker _pubSubBroker;

    public RedisController(
        RedisRepository redisRepository,
        ProductRepository productRepository,
        RedisPubSubBroker pubSubBroker)
    {
        _redisRepository = redisRepository;
        _productRepository = productRepository;
        _pubSubBroker = pubSubBroker;
    }

    [HttpGet("ping")]
    public async Task<ActionResult<RedisPingResponse>> Ping()
    {
        return Ok(await _redisRepository.PingAsync());
    }

    [HttpGet("overview")]
    public async Task<ActionResult<RedisOverviewResponse>> Overview()
    {
        return Ok(await _redisRepository.GetOverviewAsync());
    }

    [HttpGet("details")]
    public async Task<ActionResult<RedisDashboardDetailsResponse>> Details()
    {
        return Ok(await _redisRepository.GetDashboardDetailsAsync());
    }

    [HttpGet("infrastructure")]
    public async Task<ActionResult<RedisInfrastructureResponse>> Infrastructure()
    {
        return Ok(await _redisRepository.GetInfrastructureAsync());
    }

    [HttpPost("persistence/prepare")]
    public async Task<ActionResult<RedisPersistenceTestResponse>> PreparePersistenceTest()
    {
        return Ok(await _redisRepository.PreparePersistenceTestAsync());
    }

    [HttpGet("persistence/check")]
    public async Task<ActionResult<RedisPersistenceTestResponse>> CheckPersistenceTest()
    {
        return Ok(await _redisRepository.CheckPersistenceTestAsync());
    }

    [HttpDelete("persistence/clear")]
    public async Task<IActionResult> ClearPersistenceTest()
    {
        await _redisRepository.ClearPersistenceTestAsync();
        return NoContent();
    }

    [HttpGet("ranking/products")]
    public async Task<ActionResult<List<RankingItemDto>>> ProductRanking()
    {
        var products = await _productRepository.GetProductsAsync();
        return Ok(await _redisRepository.GetRankingAsync(products));
    }

    [HttpGet("ranking/product-views")]
    public async Task<ActionResult<List<RankingItemDto>>> ProductViewRanking()
    {
        var products = await _productRepository.GetProductsAsync();
        return Ok(await _redisRepository.GetProductViewRankingAsync(products));
    }

    [HttpGet("streams/orders")]
    public async Task<ActionResult<List<StreamMessageDto>>> OrderStream()
    {
        return Ok(await _redisRepository.GetOrderStreamMessagesAsync());
    }

    [HttpPost("pubsub/publish")]
    public async Task<ActionResult<object>> Publish(PublishMessageRequest request)
    {
        var subscribers = await _redisRepository.PublishAsync(request);
        return Ok(new
        {
            channel = request.Channel,
            message = request.Message,
            redisSubscribers = subscribers,
            sseClients = _pubSubBroker.ConnectedClients
        });
    }

    [HttpGet("pubsub/notifications")]
    public async Task Notifications(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.ContentType = "text/event-stream";
        var events = await _pubSubBroker.RegisterClientAsync(cancellationToken);

        try
        {
            await Response.WriteAsync("event: ready\n", cancellationToken);
            await Response.WriteAsync($"data: {{\"message\":\"connected\",\"sseClients\":{_pubSubBroker.ConnectedClients}}}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            await foreach (var payload in events.ReadAllAsync(cancellationToken))
            {
                await Response.WriteAsync("event: notification\n", cancellationToken);
                await Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Browser closed the SSE connection.
        }
    }
}

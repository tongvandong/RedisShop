using Microsoft.AspNetCore.Mvc;
using RedisDemo.Api.Dtos;
using RedisDemo.Api.Repositories;
using StackExchange.Redis;

namespace RedisDemo.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly OrderRepository _orderRepository;
    private readonly RedisRepository _redisRepository;

    public OrdersController(OrderRepository orderRepository, RedisRepository redisRepository)
    {
        _orderRepository = orderRepository;
        _redisRepository = redisRepository;
    }

    [HttpGet]
    public async Task<ActionResult<List<OrderDto>>> GetOrders()
    {
        var orders = await _orderRepository.GetOrdersAsync();
        return Ok(orders);
    }

    [HttpPost]
    public async Task<ActionResult<OrderCreatedResponse>> CreateOrder(CreateOrderRequest request)
    {
        var sessionResult = await TrySessionMatchesUserAsync(request.UserId);
        if (sessionResult.RedisOffline)
        {
            return RedisUnavailable("Redis đang offline nên chưa thể xác thực session và xử lý đơn hàng.");
        }

        if (!sessionResult.Matches)
        {
            return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ cho đơn hàng này." });
        }

        try
        {
            var order = await _orderRepository.CreateOrderAsync(request);
            await _redisRepository.ClearProductsCacheAsync();
            foreach (var item in request.Items)
            {
                await _redisRepository.ClearProductCacheAsync(item.ProductId);
            }

            var streamId = await _redisRepository.AddOrderStreamMessageAsync(order);
            await _redisRepository.ClearCartAsync(request.UserId);
            order.StreamHint = $"XADD stream:orders {streamId}";
            return Created($"/api/orders/{order.OrderId}", order);
        }
        catch (RedisException)
        {
            return RedisUnavailable("Redis đang offline nên chưa thể ghi stream/ranking/cart cho đơn hàng.");
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private async Task<(bool Matches, bool RedisOffline)> TrySessionMatchesUserAsync(long userId)
    {
        try
        {
            var sessionId = Request.Headers.TryGetValue("X-Session-Id", out var value)
                ? value.ToString()
                : "";
            var session = await _redisRepository.GetSessionAsync(sessionId);
            return (session is not null && session.UserId == userId, false);
        }
        catch (RedisException)
        {
            return (false, true);
        }
    }

    private ObjectResult RedisUnavailable(string message)
    {
        return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message });
    }
}

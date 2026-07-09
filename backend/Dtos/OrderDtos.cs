namespace RedisDemo.Api.Dtos;

public sealed class CreateOrderRequest
{
    public long UserId { get; set; }
    public List<CreateOrderItemRequest> Items { get; set; } = [];
}

public sealed class CreateOrderItemRequest
{
    public long ProductId { get; set; }
    public int Quantity { get; set; }
}

public sealed class OrderDto
{
    public long Id { get; set; }
    public string OrderCode { get; set; } = "";
    public string Username { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
    public int TotalQuantity { get; set; }
    public string StreamId { get; set; } = "";
    public string Worker { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class OrderCreatedResponse
{
    public long OrderId { get; set; }
    public string OrderCode { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "";
    public string StreamHint { get; set; } = "";
}

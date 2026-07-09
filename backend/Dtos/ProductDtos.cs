namespace RedisDemo.Api.Dtos;

public sealed class ProductDto
{
    public long Id { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string Source { get; set; } = "MYSQL";
    public int Ttl { get; set; }
    public int Score { get; set; }
}

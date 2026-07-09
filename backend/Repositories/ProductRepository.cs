using MySqlConnector;
using RedisDemo.Api.Data;
using RedisDemo.Api.Dtos;

namespace RedisDemo.Api.Repositories;

public sealed class ProductRepository
{
    private readonly MySqlConnectionFactory _connectionFactory;

    public ProductRepository(MySqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<ProductDto>> GetProductsAsync()
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        const string sql = """
            SELECT
                p.id,
                p.sku,
                p.name,
                p.description,
                p.category,
                p.price,
                p.stock_quantity,
                COALESCE(r.sales_count, 0) AS sales_count
            FROM products p
            LEFT JOIN v_product_sales_ranking r ON r.product_id = p.id
            WHERE p.is_active = TRUE
            ORDER BY p.id;
            """;

        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var products = new List<ProductDto>();
        while (await reader.ReadAsync())
        {
            products.Add(new ProductDto
            {
                Id = reader.GetInt64("id"),
                Sku = reader.GetString("sku"),
                Name = reader.GetString("name"),
                Description = reader.IsDBNull(reader.GetOrdinal("description"))
                    ? null
                    : reader.GetString("description"),
                Category = reader.GetString("category"),
                Price = reader.GetDecimal("price"),
                Stock = reader.GetInt32("stock_quantity"),
                Source = "MYSQL",
                Ttl = 0,
                Score = reader.GetInt32("sales_count")
            });
        }

        return products;
    }

    public async Task<ProductDto?> GetProductByIdAsync(long productId)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        const string sql = """
            SELECT
                p.id,
                p.sku,
                p.name,
                p.description,
                p.category,
                p.price,
                p.stock_quantity,
                COALESCE(r.sales_count, 0) AS sales_count
            FROM products p
            LEFT JOIN v_product_sales_ranking r ON r.product_id = p.id
            WHERE p.is_active = TRUE AND p.id = @productId
            LIMIT 1;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@productId", productId);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new ProductDto
        {
            Id = reader.GetInt64("id"),
            Sku = reader.GetString("sku"),
            Name = reader.GetString("name"),
            Description = reader.IsDBNull(reader.GetOrdinal("description"))
                ? null
                : reader.GetString("description"),
            Category = reader.GetString("category"),
            Price = reader.GetDecimal("price"),
            Stock = reader.GetInt32("stock_quantity"),
            Source = "MYSQL",
            Ttl = 0,
            Score = reader.GetInt32("sales_count")
        };
    }
}

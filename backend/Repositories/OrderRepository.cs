using MySqlConnector;
using RedisDemo.Api.Data;
using RedisDemo.Api.Dtos;

namespace RedisDemo.Api.Repositories;

public sealed class OrderRepository
{
    private readonly MySqlConnectionFactory _connectionFactory;

    public OrderRepository(MySqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<OrderDto>> GetOrdersAsync()
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        const string sql = """
            SELECT
                id,
                order_code,
                username,
                status,
                total_amount,
                item_count,
                total_quantity,
                created_at
            FROM v_order_summary
            ORDER BY id DESC;
            """;

        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var orders = new List<OrderDto>();
        while (await reader.ReadAsync())
        {
            var orderId = reader.GetInt64("id");
            orders.Add(new OrderDto
            {
                Id = orderId,
                OrderCode = reader.GetString("order_code"),
                Username = reader.GetString("username"),
                Status = reader.GetString("status"),
                TotalAmount = reader.GetDecimal("total_amount"),
                ItemCount = reader.GetInt32("item_count"),
                TotalQuantity = reader.GetInt32("total_quantity"),
                StreamId = $"mysql-{orderId}",
                Worker = reader.GetString("status") == "queued" ? "Waiting" : "Worker-1",
                CreatedAt = reader.GetDateTime("created_at")
            });
        }

        return orders;
    }

    public async Task<OrderCreatedResponse> CreateOrderAsync(CreateOrderRequest request)
    {
        if (request.UserId <= 0)
        {
            throw new InvalidOperationException("Thiếu thông tin người dùng.");
        }

        if (request.Items.Count == 0)
        {
            throw new InvalidOperationException("Đơn hàng phải có ít nhất một sản phẩm.");
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var productRows = await LoadProductsForOrderAsync(connection, transaction, request.Items);
            if (productRows.Count != request.Items.Select(item => item.ProductId).Distinct().Count())
            {
                throw new InvalidOperationException("Một hoặc nhiều sản phẩm không tồn tại.");
            }

            decimal totalAmount = 0;
            foreach (var item in request.Items)
            {
                if (item.Quantity <= 0)
                {
                    throw new InvalidOperationException("Số lượng phải lớn hơn 0.");
                }

                var product = productRows[item.ProductId];
                totalAmount += product.Price * item.Quantity;
            }

            var orderCode = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
            var orderId = await InsertOrderAsync(connection, transaction, request.UserId, orderCode, totalAmount);

            foreach (var item in request.Items)
            {
                var product = productRows[item.ProductId];
                await DecreaseProductStockAsync(connection, transaction, item.ProductId, item.Quantity);
                await InsertOrderItemAsync(connection, transaction, orderId, item.ProductId, product.Name, product.Price, item.Quantity);
            }

            await transaction.CommitAsync();

            return new OrderCreatedResponse
            {
                OrderId = orderId,
                OrderCode = orderCode,
                TotalAmount = totalAmount,
                Status = "queued",
                StreamHint = $"stream:orders pending for order {orderId}"
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> MarkOrderProcessedAsync(long orderId)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        const string sql = """
            UPDATE orders
            SET status = 'processed', note = 'Đã xử lý bởi Redis Stream worker'
            WHERE id = @orderId AND status IN ('queued', 'pending');
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@orderId", orderId);
        var affectedRows = await command.ExecuteNonQueryAsync();
        return affectedRows > 0;
    }

    public async Task<List<CreateOrderItemRequest>> GetOrderItemsForRankingAsync(long orderId)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        const string sql = """
            SELECT product_id, quantity
            FROM order_items
            WHERE order_id = @orderId;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@orderId", orderId);
        await using var reader = await command.ExecuteReaderAsync();

        var items = new List<CreateOrderItemRequest>();
        while (await reader.ReadAsync())
        {
            items.Add(new CreateOrderItemRequest
            {
                ProductId = reader.GetInt64("product_id"),
                Quantity = reader.GetInt32("quantity")
            });
        }

        return items;
    }

    public async Task<Dictionary<long, string>> GetOrderStatusesAsync(IEnumerable<long> orderIds)
    {
        var ids = orderIds
            .Where(orderId => orderId > 0)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        var parameterNames = ids
            .Select((_, index) => $"@orderId{index}")
            .ToList();

        var sql = $"""
            SELECT id, status
            FROM orders
            WHERE id IN ({string.Join(", ", parameterNames)});
            """;

        await using var command = new MySqlCommand(sql, connection);
        for (var index = 0; index < ids.Count; index++)
        {
            command.Parameters.AddWithValue(parameterNames[index], ids[index]);
        }

        await using var reader = await command.ExecuteReaderAsync();
        var statuses = new Dictionary<long, string>();
        while (await reader.ReadAsync())
        {
            statuses[reader.GetInt64("id")] = reader.GetString("status");
        }

        return statuses;
    }

    private static async Task<Dictionary<long, ProductForOrder>> LoadProductsForOrderAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        List<CreateOrderItemRequest> items)
    {
        var productIds = items
            .Select(item => item.ProductId)
            .Distinct()
            .ToList();

        var parameterNames = productIds
            .Select((_, index) => $"@productId{index}")
            .ToList();

        var sql = $"""
            SELECT id, name, price, stock_quantity
            FROM products
            WHERE is_active = TRUE AND id IN ({string.Join(", ", parameterNames)});
            """;

        await using var command = new MySqlCommand(sql, connection, transaction);
        for (var index = 0; index < productIds.Count; index++)
        {
            command.Parameters.AddWithValue(parameterNames[index], productIds[index]);
        }

        await using var reader = await command.ExecuteReaderAsync();
        var products = new Dictionary<long, ProductForOrder>();
        while (await reader.ReadAsync())
        {
            products[reader.GetInt64("id")] = new ProductForOrder
            {
                Name = reader.GetString("name"),
                Price = reader.GetDecimal("price"),
                StockQuantity = reader.GetInt32("stock_quantity")
            };
        }

        foreach (var item in items)
        {
            if (products.TryGetValue(item.ProductId, out var product) && item.Quantity > product.StockQuantity)
            {
                throw new InvalidOperationException($"Sản phẩm {item.ProductId} không đủ tồn kho.");
            }
        }

        return products;
    }

    private static async Task<long> InsertOrderAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        long userId,
        string orderCode,
        decimal totalAmount)
    {
        const string sql = """
            INSERT INTO orders (order_code, user_id, status, total_amount, note)
            VALUES (@orderCode, @userId, 'queued', @totalAmount, 'Tạo từ React shop');
            SELECT LAST_INSERT_ID();
            """;

        await using var command = new MySqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@orderCode", orderCode);
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.AddWithValue("@totalAmount", totalAmount);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }

    private static async Task InsertOrderItemAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        long orderId,
        long productId,
        string productName,
        decimal unitPrice,
        int quantity)
    {
        const string sql = """
            INSERT INTO order_items (order_id, product_id, product_name_snapshot, unit_price, quantity)
            VALUES (@orderId, @productId, @productName, @unitPrice, @quantity);
            """;

        await using var command = new MySqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@productId", productId);
        command.Parameters.AddWithValue("@productName", productName);
        command.Parameters.AddWithValue("@unitPrice", unitPrice);
        command.Parameters.AddWithValue("@quantity", quantity);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DecreaseProductStockAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        long productId,
        int quantity)
    {
        const string sql = """
            UPDATE products
            SET stock_quantity = stock_quantity - @quantity
            WHERE id = @productId
                AND is_active = TRUE
                AND stock_quantity >= @quantity;
            """;

        await using var command = new MySqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@productId", productId);
        command.Parameters.AddWithValue("@quantity", quantity);

        var affectedRows = await command.ExecuteNonQueryAsync();
        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"Sản phẩm {productId} không đủ tồn kho.");
        }
    }

    private sealed class ProductForOrder
    {
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
    }
}

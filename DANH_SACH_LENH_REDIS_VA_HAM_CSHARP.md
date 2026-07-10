# Danh Sách Lệnh Redis Và Hàm C# Tương Ứng

Tài liệu này liệt kê các lệnh Redis thật được backend C# gửi tới Redis Server Ubuntu thông qua thư viện `StackExchange.Redis`.

File chính chứa các thao tác Redis:

```text
backend/Repositories/RedisRepository.cs
```

## 1. Kết Nối / Kiểm Tra Redis

| Module | Redis command thật | Hàm C# | File | Dòng |
|---|---|---|---|---:|
| Ping Redis | `PING` | `PingAsync()` | `backend/Repositories/RedisRepository.cs` | 53 |

## 2. Cache Danh Sách Sản Phẩm

| Module | Redis command thật | Hàm C# | File | Dòng |
|---|---|---|---|---:|
| Đọc cache danh sách sản phẩm | `GET cache:products` | `GetProductsFromCacheAsync(...)` | `backend/Repositories/RedisRepository.cs` | 68 |
| Ghi nhận cache hit/miss | `INCR metrics:cache:products:hit`, `INCR metrics:cache:products:miss` | `GetProductsFromCacheAsync(...)` | `backend/Repositories/RedisRepository.cs` | 68 |
| Đọc TTL cache danh sách | `TTL cache:products` | `GetProductsFromCacheAsync(...)` | `backend/Repositories/RedisRepository.cs` | 68 |
| Ghi cache danh sách | `SET cache:products <json> EX <ttl>` | `SaveProductsCacheAsync(...)` | `backend/Repositories/RedisRepository.cs` | 185 |
| Ghi metric cache | `SET metrics:cache:products:* <value>` | `SaveProductsCacheAsync(...)` | `backend/Repositories/RedisRepository.cs` | 185 |
| Xóa cache danh sách và cache chi tiết | `DEL cache:products`, `DEL cache:product:*` | `ClearProductsCacheAsync()` | `backend/Repositories/RedisRepository.cs` | 416 |
| Đánh dấu trạng thái đã xóa cache | `SET metrics:cache:products:last-source CLEARED` | `ClearProductsCacheAsync()` | `backend/Repositories/RedisRepository.cs` | 416 |

## 3. Cache Chi Tiết Sản Phẩm

| Module | Redis command thật | Hàm C# | File | Dòng |
|---|---|---|---|---:|
| Đọc cache chi tiết | `GET cache:product:{id}` | `GetProductFromCacheAsync(...)` | `backend/Repositories/RedisRepository.cs` | 122 |
| Đọc TTL cache chi tiết | `TTL cache:product:{id}` | `GetProductFromCacheAsync(...)` | `backend/Repositories/RedisRepository.cs` | 122 |
| Tăng lượt xem sản phẩm | `ZINCRBY ranking:product:views 1 {id}` | `GetProductFromCacheAsync(...)` | `backend/Repositories/RedisRepository.cs` | 122 |
| Ghi cache chi tiết | `SET cache:product:{id} <json> EX <ttl>` | `SaveProductCacheAsync(...)` | `backend/Repositories/RedisRepository.cs` | 158 |
| Tăng lượt xem sau khi rebuild cache | `ZINCRBY ranking:product:views 1 {id}` | `SaveProductCacheAsync(...)` | `backend/Repositories/RedisRepository.cs` | 158 |
| Xóa cache chi tiết một sản phẩm | `DEL cache:product:{id}` | `ClearProductCacheAsync(...)` | `backend/Repositories/RedisRepository.cs` | 450 |

## 4. Chống Cache Stampede

| Module | Redis command thật | Hàm C# | File | Dòng |
|---|---|---|---|---:|
| Lấy lock rebuild danh sách | `SET lock:cache:products:rebuild <token> EX 5 NX` | `TryAcquireProductsCacheRebuildLockAsync()` | `backend/Repositories/RedisRepository.cs` | 222 |
| Nhả lock rebuild danh sách an toàn | `EVAL <Lua compare-token-then-DEL>` | `ReleaseProductsCacheRebuildLockAsync(...)` | `backend/Repositories/RedisRepository.cs` | 250 |
| Lấy lock rebuild chi tiết | `SET lock:cache:product:{id}:rebuild <token> EX 5 NX` | `TryAcquireProductCacheRebuildLockAsync(...)` | `backend/Repositories/RedisRepository.cs` | 276 |
| Nhả lock rebuild chi tiết an toàn | `EVAL <Lua compare-token-then-DEL>` | `ReleaseProductCacheRebuildLockAsync(...)` | `backend/Repositories/RedisRepository.cs` | 304 |
| Chờ request khác rebuild cache chi tiết | `GET cache:product:{id}` lặp lại trong thời gian chờ | `WaitForProductCacheAsync(...)` | `backend/Repositories/RedisRepository.cs` | 330 |
| Chờ request khác rebuild cache danh sách | `GET cache:products` lặp lại trong thời gian chờ | `WaitForProductsCacheAsync(...)` | `backend/Repositories/RedisRepository.cs` | 353 |
| Đọc cache không tăng miss khi đang chờ lock | `GET cache:products`, `INCR metrics:cache:products:hit`, `TTL cache:products` | `ReadProductsCacheWithoutMissAsync(...)` | `backend/Repositories/RedisRepository.cs` | 376 |

## 5. Session Store

| Module | Redis command thật | Hàm C# | File | Dòng |
|---|---|---|---|---:|
| Tạo session đăng nhập | `HSET session:{id} userId username role createdAt` | `CreateSessionAsync(...)` | `backend/Repositories/RedisRepository.cs` | 469 |
| Gán TTL session | `EXPIRE session:{id} <ttl>` | `CreateSessionAsync(...)` | `backend/Repositories/RedisRepository.cs` | 469 |
| Đọc session | `HGETALL session:{id}` | `GetSessionAsync(...)` | `backend/Repositories/RedisRepository.cs` | 490 |
| Đọc TTL session | `TTL session:{id}` | `GetSessionAsync(...)` | `backend/Repositories/RedisRepository.cs` | 490 |
| Logout / xóa session | `DEL session:{id}` | `DeleteSessionAsync(...)` | `backend/Repositories/RedisRepository.cs` | 519 |

## 6. Shopping Cart

| Module | Redis command thật | Hàm C# | File | Dòng |
|---|---|---|---|---:|
| Đọc giỏ hàng | `HGETALL cart:{userId}` | `GetCartAsync(...)` | `backend/Repositories/RedisRepository.cs` | 530 |
| Đọc TTL giỏ hàng | `TTL cart:{userId}` | `GetCartAsync(...)` | `backend/Repositories/RedisRepository.cs` | 530 |
| Thêm/cập nhật item trong giỏ | `HSET cart:{userId} {productId} {quantity}` | `SaveCartItemAsync(...)` | `backend/Repositories/RedisRepository.cs` | 547 |
| Xóa item khỏi giỏ khi quantity bằng 0 | `HDEL cart:{userId} {productId}` | `SaveCartItemAsync(...)` | `backend/Repositories/RedisRepository.cs` | 547 |
| Gia hạn TTL giỏ hàng | `EXPIRE cart:{userId} <ttl>` | `SaveCartItemAsync(...)` | `backend/Repositories/RedisRepository.cs` | 547 |
| Xóa giỏ hàng sau khi đặt hàng | `DEL cart:{userId}` | `ClearCartAsync(...)` | `backend/Repositories/RedisRepository.cs` | 566 |

## 7. Rate Limiting

| Module | Redis command thật | Hàm C# | File | Dòng |
|---|---|---|---|---:|
| Tăng counter login | `INCR rate:login:ip:{ip}:user:{username}` | `CheckRateLimitAsync(...)` | `backend/Repositories/RedisRepository.cs` | 572 |
| Gán thời gian sống cho counter | `EXPIRE rate:login:ip:{ip}:user:{username} <window>` | `CheckRateLimitAsync(...)` | `backend/Repositories/RedisRepository.cs` | 572 |
| Đọc TTL counter | `TTL rate:login:ip:{ip}:user:{username}` | `CheckRateLimitAsync(...)` | `backend/Repositories/RedisRepository.cs` | 572 |

## 8. Ranking Bán Chạy

| Module | Redis command thật | Hàm C# | File | Dòng |
|---|---|---|---|---:|
| Đánh dấu order đã cộng ranking | `SADD ranking:processed-orders {orderId}` trong Lua | `IncrementProductRankingForOrderAsync(...)` | `backend/Repositories/RedisRepository.cs` | 594 |
| Tăng điểm bán chạy | `ZINCRBY ranking:products {quantity} {productId}` trong Lua | `IncrementProductRankingForOrderAsync(...)` | `backend/Repositories/RedisRepository.cs` | 594 |
| Đọc ranking bán chạy | `ZREVRANGE ranking:products 0 <take-1> WITHSCORES` | `GetRankingAsync(...)` | `backend/Repositories/RedisRepository.cs` | 632 |

## 9. Ranking Lượt Xem

| Module | Redis command thật | Hàm C# | File | Dòng |
|---|---|---|---|---:|
| Tăng lượt xem sản phẩm | `ZINCRBY ranking:product:views 1 {id}` | `GetProductFromCacheAsync(...)` / `SaveProductCacheAsync(...)` | `backend/Repositories/RedisRepository.cs` | 122 / 158 |
| Đọc ranking lượt xem | `ZREVRANGE ranking:product:views 0 <take-1> WITHSCORES` | `GetProductViewRankingAsync(...)` | `backend/Repositories/RedisRepository.cs` | 655 |

## 10. Redis Streams

| Module | Redis command thật | Hàm C# | File | Dòng |
|---|---|---|---|---:|
| Ghi event order vào stream | `XADD stream:orders * orderId <id> orderCode <code> status <status> totalAmount <amount>` | `AddOrderStreamMessageAsync(...)` | `backend/Repositories/RedisRepository.cs` | 678 |
| Đọc message mới nhất | `XREVRANGE stream:orders + - COUNT <take>` | `GetOrderStreamMessagesAsync(...)` | `backend/Repositories/RedisRepository.cs` | 694 |
| Tạo consumer group | `XGROUP CREATE stream:orders order-workers 0 MKSTREAM` | `EnsureOrderConsumerGroupAsync()` | `backend/Repositories/RedisRepository.cs` | 722 |
| Worker đọc message | `XREADGROUP GROUP order-workers {consumerName} COUNT <count> STREAMS stream:orders >` | `ReadOrderStreamAsync(...)` | `backend/Repositories/RedisRepository.cs` | 741 |
| Kiểm tra pending message | `XPENDING stream:orders order-workers` | `ClaimStaleOrderStreamMessagesAsync(...)` | `backend/Repositories/RedisRepository.cs` | 753 |
| Claim message bị treo | `XCLAIM stream:orders order-workers {consumerName} <min-idle-ms> <messageId>` | `ClaimStaleOrderStreamMessagesAsync(...)` | `backend/Repositories/RedisRepository.cs` | 753 |
| Xác nhận xử lý xong | `XACK stream:orders order-workers <messageId>` | `AckOrderStreamAsync(...)` | `backend/Repositories/RedisRepository.cs` | 786 |

## 11. Pub/Sub Realtime

| Module | Redis command thật | Hàm C# | File | Dòng |
|---|---|---|---|---:|
| Publish thông báo realtime | `PUBLISH notifications <message>` | `PublishAsync(...)` | `backend/Repositories/RedisRepository.cs` | 799 |

## 12. Dashboard Overview / Monitor

| Module | Redis command thật | Hàm C# | File | Dòng |
|---|---|---|---|---:|
| Lấy thông tin Redis tổng quan | `INFO` | `GetOverviewAsync()` | `backend/Repositories/RedisRepository.cs` | 813 |
| Đếm tổng số key | `DBSIZE` | `GetOverviewAsync()` | `backend/Repositories/RedisRepository.cs` | 813 |
| Đếm session/cart/rate key | `SCAN session:*`, `SCAN cart:*`, `SCAN rate:*` | `GetOverviewAsync()` | `backend/Repositories/RedisRepository.cs` | 813 |
| Đếm stream length | `XLEN stream:orders` | `GetOverviewAsync()` | `backend/Repositories/RedisRepository.cs` | 813 |
| Đọc chi tiết dashboard | `SCAN`, `HGETALL`, `TTL`, `XPENDING`, `XINFO GROUPS` | `GetDashboardDetailsAsync()` | `backend/Repositories/RedisRepository.cs` | 836 |
| Đọc metric cache | `GET metrics:cache:products:*`, `TTL cache:products` | `GetCacheMonitorAsync()` | `backend/Repositories/RedisRepository.cs` | 1044 |
| Đọc session monitor | `SCAN session:*`, `HGETALL session:{id}`, `TTL session:{id}` | `GetSessionMonitorsAsync(...)` | `backend/Repositories/RedisRepository.cs` | 1135 |
| Đọc cart monitor | `SCAN cart:*`, `HGETALL cart:{userId}`, `TTL cart:{userId}` | `GetCartMonitorsAsync(...)` | `backend/Repositories/RedisRepository.cs` | 1159 |
| Đọc rate limit monitor | `SCAN rate:*`, `GET rate:{...}`, `TTL rate:{...}` | `GetRateLimitMonitorsAsync(...)` | `backend/Repositories/RedisRepository.cs` | 1203 |
| Đọc stream monitor | `XLEN stream:orders`, `XPENDING stream:orders order-workers`, `XINFO GROUPS stream:orders` | `GetStreamMonitorAsync(...)` | `backend/Repositories/RedisRepository.cs` | 1227 |

## 13. Redis Infrastructure / HA

| Module | Redis command thật | Hàm C# | File | Dòng |
|---|---|---|---|---:|
| Hỏi Sentinel current master | `SENTINEL get-master-addr-by-name mymaster` | `GetInfrastructureAsync()` | `backend/Repositories/RedisRepository.cs` | 851 |
| Đọc INFO hạ tầng | `INFO` | `GetInfrastructureAsync()` | `backend/Repositories/RedisRepository.cs` | 851 |
| Đọc policy/maxmemory/AOF/save | `CONFIG GET maxmemory-policy`, `CONFIG GET maxmemory`, `CONFIG GET appendonly`, `CONFIG GET save` | `GetInfrastructureAsync()` | `backend/Repositories/RedisRepository.cs` | 851 |
| Đọc từng Redis node | `INFO replication/server/persistence/memory`, `CONFIG GET maxmemory-policy|maxmemory` | `BuildNodeStatusesAsync(...)` | `backend/Repositories/RedisRepository.cs` | 886 |
| Đọc trạng thái một node cụ thể | `INFO`, `CONFIG GET maxmemory-policy`, `CONFIG GET maxmemory` | `ReadNodeStatusAsync(...)` | `backend/Repositories/RedisRepository.cs` | 909 |

## 14. Persistence Test 6381

| Module | Redis command thật | Hàm C# | File | Dòng |
|---|---|---|---|---:|
| Tạo string test | `SET persistence:test:cache <value>` | `PreparePersistenceTestAsync()` | `backend/Repositories/RedisRepository.cs` | 985 |
| Tạo hash session test | `HSET persistence:test:session ...` | `PreparePersistenceTestAsync()` | `backend/Repositories/RedisRepository.cs` | 985 |
| Tạo hash cart test | `HSET persistence:test:cart ...` | `PreparePersistenceTestAsync()` | `backend/Repositories/RedisRepository.cs` | 985 |
| Tạo sorted set ranking test | `ZADD persistence:test:ranking ...` | `PreparePersistenceTestAsync()` | `backend/Repositories/RedisRepository.cs` | 985 |
| Tạo stream test | `XADD persistence:test:stream * ...` | `PreparePersistenceTestAsync()` | `backend/Repositories/RedisRepository.cs` | 985 |
| Ép Redis ghi RDB snapshot | `BGSAVE` | `PreparePersistenceTestAsync()` | `backend/Repositories/RedisRepository.cs` | 985 |
| Kiểm tra số key | `DBSIZE`, `EXISTS`, `TYPE`, `TTL` | `CheckPersistenceTestAsync()` / `BuildPersistenceTestResponseAsync(...)` | `backend/Repositories/RedisRepository.cs` | 1029 / 1066 |
| Xem preview giá trị string | `GET persistence:test:cache` | `BuildPersistenceTestResponseAsync(...)` | `backend/Repositories/RedisRepository.cs` | 1066 |
| Xem preview hash | `HGETALL persistence:test:session`, `HGETALL persistence:test:cart` | `BuildPersistenceTestResponseAsync(...)` | `backend/Repositories/RedisRepository.cs` | 1066 |
| Xem preview sorted set | `ZRANGE persistence:test:ranking 0 -1 WITHSCORES` | `BuildPersistenceTestResponseAsync(...)` | `backend/Repositories/RedisRepository.cs` | 1066 |
| Xem preview stream | `XRANGE persistence:test:stream - +` | `BuildPersistenceTestResponseAsync(...)` | `backend/Repositories/RedisRepository.cs` | 1066 |
| Xóa dữ liệu test | `DEL persistence:test:cache persistence:test:session persistence:test:cart persistence:test:ranking persistence:test:stream` | `ClearPersistenceTestAsync()` | `backend/Repositories/RedisRepository.cs` | 1037 |

## 15. Ghi Chú Về Các File Liên Quan

| File | Vai trò |
|---|---|
| `backend/Data/RedisConnectionFactory.cs` | Tạo kết nối thật tới Redis HA `6379`, Redis Cache `6380`, Redis Persistence `6381`, và Sentinel. |
| `backend/Repositories/RedisRepository.cs` | Chứa phần lớn lệnh Redis thật được gửi qua `StackExchange.Redis`. |
| `backend/Controllers/RedisController.cs` | Expose API cho Redis Dashboard gọi các hàm trong `RedisRepository`. |
| `backend/Controllers/ProductsController.cs` | Gọi các hàm cache sản phẩm, cache lock, cache invalidation. |
| `backend/Controllers/AuthController.cs` | Gọi session store và rate limit. |
| `backend/Controllers/CartController.cs` | Gọi Redis Hash cart. |
| `backend/Controllers/OrdersController.cs` | Tạo order, clear cart, invalidation cache, và ghi event vào Redis Stream. |
| `backend/Workers/OrderStreamWorker.cs` | Worker đọc Redis Stream bằng Consumer Group và ACK message sau khi xử lý. |

## 16. Cách Đọc Bảng Này Khi Giải Thích

Ví dụ:

```text
SaveCartItemAsync ở RedisRepository.cs dòng 547
```

Hàm này dùng:

```redis
HSET cart:{userId} {productId} {quantity}
EXPIRE cart:{userId} <ttl>
```

Nghĩa là khi người dùng thêm sản phẩm vào giỏ, backend C# gửi lệnh Redis thật tới Redis Server Ubuntu qua `StackExchange.Redis`, chứ không phải giả lập trong frontend.

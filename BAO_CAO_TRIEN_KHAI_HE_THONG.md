# Báo Cáo Triển Khai Hệ Thống Redis Demo

## 1. Tổng Quan

Hệ thống Redis Demo mô phỏng một ứng dụng bán hàng trực tuyến có backend C#, MySQL và Redis chạy trên Ubuntu VM. Shop dùng để tạo nghiệp vụ thật, còn Redis Dashboard dùng để quan sát và chứng minh các cơ chế Redis.

Các thành phần chính:

- `database`: schema MySQL, seed data, tài liệu dữ liệu.
- `backend`: ASP.NET Core Web API kết nối MySQL, Redis và Redis Sentinel.
- `frontend`: Shop React, chạy ở `http://127.0.0.1:5173`.
- `redis-dashboard`: Dashboard kỹ thuật Redis, chạy ở `http://127.0.0.1:5174`.

Backend chạy ở:

```text
http://127.0.0.1:5000
```

Redis HA hiện dùng 3 VM:

```text
192.168.203.128:6379 / 26379
192.168.203.129:6379 / 26379
192.168.203.130:6379 / 26379
```

Sentinel service name:

```text
mymaster
```

Redis đã tách theo vai trò:

```text
6379 = Redis HA qua Sentinel, dùng cho session/cart/rate limit/ranking/stream/pubsub
6380 = Redis Cache riêng, dùng cho product cache và cache monitor
6381 = Redis Persistence Test riêng, standalone, dùng cho RDB/AOF test
```

## 2. Công Nghệ Sử Dụng

Frontend:

- React
- Vite
- React Router
- Lucide React

Backend:

- ASP.NET Core Web API .NET 8
- MySqlConnector
- StackExchange.Redis
- Hosted Service cho Redis Stream worker
- Server-Sent Events cho realtime notification

Database:

- MySQL
- Redis Server trên Ubuntu VM
- Redis Replication
- Redis Sentinel

## 3. MySQL Database

Các bảng chính:

- `users`
- `products`
- `orders`
- `order_items`

Vai trò MySQL:

- Lưu user và thông tin đăng nhập.
- Lưu sản phẩm.
- Lưu đơn hàng và chi tiết đơn hàng.
- Trừ tồn kho thật trong transaction khi tạo đơn.
- MySQL là source of truth cho dữ liệu nghiệp vụ lâu dài như người dùng, sản phẩm và đơn hàng.
- Redis đảm nhiệm các workload cần tốc độ cao và cấu trúc dữ liệu chuyên dụng như cache, session, cart, rate limiting, ranking, stream processing và pub/sub realtime.

Các script:

```text
database/sql/00_reset.sql
database/sql/01_schema.sql
database/sql/02_seed.sql
database/sql/03_verify.sql
database/sql/04_update_vietnamese_text.sql
```

## 4. Backend ASP.NET Core

Các nhóm file chính:

```text
Controllers/
  AuthController.cs
  ProductsController.cs
  CartController.cs
  OrdersController.cs
  RedisController.cs
  HealthController.cs

Repositories/
  AuthRepository.cs
  ProductRepository.cs
  OrderRepository.cs
  RedisRepository.cs

Services/
  RedisPubSubBroker.cs

Workers/
  OrderStreamWorker.cs

Data/
  MySqlConnectionFactory.cs
  RedisConnectionFactory.cs
  RedisOptions.cs
```

Backend đã làm:

- Kết nối MySQL thật.
- Kết nối Redis thật.
- Kết nối Redis HA qua Sentinel.
- Tự hỏi Sentinel để lấy master hiện tại.
- Tạo đơn hàng bằng transaction MySQL, kiểm tra và trừ tồn kho thật.
- Xóa cache sản phẩm sau khi tồn kho thay đổi.
- Có fallback cho một số phần cache khi Redis offline.
- Không còn bật Swagger tự động khi chạy backend.

## 5. Cấu Hình Redis

Trong `backend/appsettings.json`:

```json
"Redis": {
  "Host": "192.168.203.128",
  "Port": 6379,
  "Password": "RedisDemo@123",
  "Database": 0,
  "Sentinel": {
    "Enabled": true,
    "ServiceName": "mymaster",
    "Endpoints": [
      { "Host": "192.168.203.128", "Port": 26379 },
      { "Host": "192.168.203.129", "Port": 26379 },
      { "Host": "192.168.203.130", "Port": 26379 }
    ]
  },
  "Cache": {
    "Enabled": true,
    "Host": "192.168.203.128",
    "Port": 6380,
    "Password": "RedisDemo@123",
    "Database": 0
  },
  "Persistence": {
    "Enabled": true,
    "Host": "192.168.203.128",
    "Port": 6381,
    "Password": "RedisDemo@123",
    "Database": 0
  },
  "ProductsCacheSeconds": 60,
  "ProductDetailCacheSeconds": 300,
  "SessionSeconds": 1800,
  "CartSeconds": 1800,
  "RateLimitSeconds": 60,
  "RateLimitMaxRequests": 5
}
```

Ý nghĩa TTL:

- `ProductsCacheSeconds = 60`: cache danh sách sản phẩm.
- `ProductDetailCacheSeconds = 300`: TTL cho cache chi tiết từng sản phẩm, kết hợp với view counter và LRU eviction trên Redis Cache `6380` để phục vụ quan sát hành vi truy cập sản phẩm.

## 6. API Đã Triển Khai

Auth:

```text
POST /api/auth/login
GET  /api/auth/me
POST /api/auth/logout
```

Products:

```text
GET    /api/products
GET    /api/products?trackMetrics=false
GET    /api/products/{productId}
DELETE /api/products/cache
```

Cart:

```text
GET    /api/users/{userId}/cart
PUT    /api/users/{userId}/cart/items/{productId}
DELETE /api/users/{userId}/cart
```

Orders:

```text
GET  /api/orders
POST /api/orders
```

Redis Dashboard:

```text
GET  /api/redis/ping
GET  /api/redis/overview
GET  /api/redis/details
GET  /api/redis/infrastructure
GET  /api/redis/ranking/products
GET  /api/redis/streams/orders
POST /api/redis/pubsub/publish
GET  /api/redis/pubsub/notifications
POST /api/redis/persistence/prepare
GET  /api/redis/persistence/check
DELETE /api/redis/persistence/clear
```

## 7. Shop Frontend

Shop chạy ở:

```text
http://127.0.0.1:5173
```

Các route chính:

```text
/shop/login
/shop/products
/shop/products/:productId
/shop/cart
/shop/orders
```

Chức năng đã có:

- Đăng nhập.
- Lưu session local.
- Gửi `X-Session-Id` cho API cần session.
- Xem danh sách sản phẩm.
- Xem trang chi tiết riêng của từng sản phẩm.
- Thêm vào giỏ hàng.
- Icon giỏ hàng hiển thị tổng số lượng sản phẩm.
- Xem giỏ hàng.
- Đặt hàng trực tiếp từ trang giỏ hàng.
- Xem lịch sử đơn hàng.
- Đăng xuất.
- Nhận thông báo realtime từ Redis Pub/Sub qua SSE.

Lưu ý UI:

- Shop không hiển thị nhãn kỹ thuật Redis/MySQL trên danh sách sản phẩm.
- Trang chi tiết sản phẩm có thể hiển thị `Nguồn dữ liệu` và `TTL cache` để phục vụ demo cache hot product.

## 8. Redis Dashboard

Dashboard chạy ở:

```text
http://127.0.0.1:5174
```

Các route:

```text
/redis/overview
/redis/streams
/redis/infrastructure
```

Dashboard tự refresh mỗi 1 giây cho metric kỹ thuật. Riêng Pub/Sub realtime dùng SSE, không phụ thuộc polling 1 giây.

Dashboard gọi danh sách sản phẩm bằng:

```text
GET /api/products?trackMetrics=false
```

Ý nghĩa:

- Dashboard chỉ quan sát dữ liệu.
- Dashboard không làm tăng HIT/MISS.
- Dashboard không làm thay đổi `Last load`.
- Metric cache phản ánh traffic thật của Shop.

## 9. Module Redis Đã Triển Khai

### 9.1 Cache-Aside Danh Sách Sản Phẩm

Key:

```text
cache:products
```

Metric keys:

```text
metrics:cache:products:hit
metrics:cache:products:miss
metrics:cache:products:last-source
metrics:cache:products:last-duration-ms
metrics:cache:products:mysql-duration-ms
metrics:cache:products:redis-duration-ms
```

Luồng:

```text
Shop gọi /api/products
Backend kiểm tra cache:products
Nếu có cache -> trả Redis
Nếu miss -> đọc MySQL -> ghi cache Redis TTL 60s
```

Đã bổ sung phân biệt traffic:

```text
Shop /api/products
-> tính metric cache thật

Dashboard /api/products?trackMetrics=false
-> chỉ quan sát, không làm đổi metric
```

### 9.2 Chống Cache Stampede Cho Danh Sách

Lock key:

```text
lock:cache:products:rebuild
```

Khi `cache:products` miss:

```text
Request đầu tiên lấy lock bằng SET NX EX
Request giữ lock query MySQL và rebuild cache
Các request khác chờ cache được rebuild
Nếu chờ quá lâu thì fallback MySQL nhưng không tranh rebuild
```

Lệnh Redis về mặt lý thuyết:

```text
SET lock:cache:products:rebuild <token> NX EX 5
```

Khi release lock, backend dùng Lua để chỉ xóa lock nếu token trùng:

```lua
if redis.call('GET', KEYS[1]) == ARGV[1] then
    return redis.call('DEL', KEYS[1])
end
return 0
```

Lý do dùng token:

- Tránh request A xóa nhầm lock của request B.
- Lock vẫn tự hết hạn nếu request bị lỗi giữa chừng.

Header kiểm thử:

```text
X-Cache-Rebuild: LOCK_OWNER
X-Cache-Rebuild: WAITED_FOR_OTHER_REQUEST
X-Cache-Rebuild: LOCK_TIMEOUT_FALLBACK_MYSQL
```

### 9.3 Cache Chi Tiết Từng Sản Phẩm

Key:

```text
cache:product:{id}
```

Ví dụ:

```text
cache:product:1
cache:product:2
cache:product:3
```

Endpoint:

```text
GET /api/products/{productId}
```

Luồng:

```text
Người dùng mở trang chi tiết sản phẩm
Backend kiểm tra cache:product:{id}
Nếu hit -> trả Redis
Nếu miss -> lấy lock riêng sản phẩm -> đọc MySQL -> ghi Redis TTL 300s
```

Lock key:

```text
lock:cache:product:{id}:rebuild
```

Ý nghĩa demo:

- Cache chi tiết được tách theo từng sản phẩm bằng key `cache:product:{id}`.
- Hệ thống kết hợp per-product cache, counter lượt xem `ranking:product:views` và Redis Cache `6380` sử dụng `allkeys-lru`.
- Cách này giúp quan sát hành vi dữ liệu được truy cập thường xuyên và dữ liệu ít được truy cập khi có áp lực bộ nhớ.

Kiểm thử thực tế:

```text
Lần 1:
X-Cache-Rebuild: LOCK_OWNER
X-Data-Source: MYSQL

Lần 2:
X-Data-Source: REDIS
Body: "source": "REDIS", "ttl": 299
```

### 9.4 Event-Based Invalidation

Khi tạo đơn hàng thành công:

```text
POST /api/orders
```

Backend xóa:

```text
cache:products
cache:product:{id} của từng sản phẩm trong đơn
```

Lý do:

- Tạo đơn thành công làm thay đổi `products.stock_quantity` trong MySQL.
- `cache:products` và `cache:product:{id}` có thể chứa tồn kho cũ.
- Request sau sẽ buộc rebuild cache từ MySQL.

Vị trí code:

```text
OrdersController.cs
```

### 9.5 Session Store

Key:

```text
session:<sessionId>
```

Đã có:

- Login tạo session Redis.
- TTL 1800 giây.
- API kiểm tra session qua `X-Session-Id`.
- Logout xóa session.

### 9.6 Shopping Cart

Key:

```text
cart:<userId>
```

Kiểu dữ liệu:

```text
Redis Hash
```

Field/value:

```text
field = productId
value = quantity
```

Đã có:

- Thêm sản phẩm.
- Tăng/giảm số lượng.
- Xóa item khi quantity về 0.
- TTL 1800 giây.
- Dashboard hiển thị chi tiết item, tổng số lượng, tổng tiền.

### 9.7 Rate Limiting

Key:

```text
rate:login:ip:<ip>:user:<username>
```

Đã có:

- Giới hạn 5 request / 60 giây.
- Key gồm cả IP và username để tránh nhiều người dùng chung mạng bị ảnh hưởng quá rộng.
- Quá giới hạn trả HTTP 429.
- Dashboard hiển thị key, count, limit, TTL, status.

### 9.8 Sorted Set Ranking

Key:

```text
ranking:products
```

Đã có:

- Khi tạo đơn thành công, backend ghi message vào Redis Stream.
- Worker xử lý message đơn hàng rồi mới tăng score theo số lượng bán.
- Cách này làm ranking phản ánh đơn đã được worker xử lý, không tăng trực tiếp ngay trong controller.
- Có key `ranking:processed-orders` để ghi nhận orderId đã được apply ranking.
- Worker dùng Lua script để `SADD ranking:processed-orders` và `ZINCRBY ranking:products` trong cùng một thao tác atomic.
- Lua script nhận toàn bộ item của một order trong cùng một lần gọi, nên không có lỗi chỉ cộng sản phẩm đầu tiên.
- Dashboard hiển thị top sản phẩm bán chạy.

Ngoài ra cache chi tiết sản phẩm có counter view:

```text
ranking:product:views
```

Dùng để chứng minh sản phẩm nào đang được xem nhiều.

### 9.9 Redis Streams

Key:

```text
stream:orders
```

Consumer group:

```text
order-workers
```

Đã có:

- Khi tạo đơn, backend ghi `XADD stream:orders`.
- Worker nền đọc bằng consumer group.
- Worker cập nhật đơn sang `processed`.
- Worker tăng `ranking:products` sau khi đơn được chuyển sang `processed`.
- Worker có bước nhận lại message pending bị treo bằng `XPENDING` + `XCLAIM`.
- Nếu message cũ được xử lý lại, `ranking:processed-orders` giúp không tăng ranking lặp cho cùng một orderId.
- Worker chỉ `XACK` sau khi đã xử lý MySQL và apply ranking idempotent thành công.
- Dashboard hiển thị stream length, pending, consumer count, last delivered ID.

### 9.10 Pub/Sub Realtime

Channel:

```text
notifications
```

Luồng hiện tại:

```text
Redis Dashboard
  POST /api/redis/pubsub/publish
ASP.NET Core Publisher
  PUBLISH notifications
Redis channel notifications
ASP.NET Core Subscriber
  SSE
├─ Redis Dashboard EventSource
└─ Customer Shop EventSource
```

Đã làm:

- Dashboard có input publish message.
- Backend publish message vào Redis Pub/Sub.
- Backend có `RedisPubSubBroker` subscribe channel `notifications`.
- Backend đẩy message xuống browser bằng SSE.
- Dashboard nhận realtime.
- Shop cũng nhận realtime khi đang đăng nhập.
- Có thể mở Dashboard và Shop ở 2 cửa sổ, publish từ Dashboard, Shop hiện thông báo mà không reload.

Điểm lý thuyết:

- Redis Pub/Sub broadcast tới subscriber đang online.
- Browser không subscribe Redis trực tiếp.
- Browser giữ kết nối SSE tới backend.
- Message không được replay cho client offline, đúng semantics at-most-once của Redis Pub/Sub.

### 9.11 Persistence Test

API:

```text
POST   /api/redis/persistence/prepare
GET    /api/redis/persistence/check
DELETE /api/redis/persistence/clear
```

Key test:

```text
persistence:test:cache
persistence:test:session
persistence:test:cart
persistence:test:ranking
persistence:test:stream
```

Quy trình:

```text
1. Tạo 5 key test.
2. Restart Redis trên Ubuntu.
3. Kiểm tra lại.
4. PASS nếu 5/5 key vẫn tồn tại.
```

Lưu ý kiến trúc:

- Bài test hiện chạy trên Redis Persistence riêng `192.168.203.128:6381`.
- Instance `6381` chạy standalone, không Sentinel và không replica.
- Khi restart `redis-persistence-6381`, 5/5 key test vẫn tồn tại, nên bài test đã cô lập khỏi replication/failover của cụm HA `6379`.

### 9.12 Redis HA / Sentinel

Đã có:

- Redis Replication với 2 replica.
- 3 Sentinel node.
- Quorum 2.
- Backend kết nối qua Sentinel.
- Backend không phụ thuộc cứng IP master cũ.
- Dashboard infrastructure đọc master hiện tại, role, replicas, Sentinel endpoints.
- Đã test failover thủ công bằng Sentinel.

Lưu ý vận hành:

- Redis HA port `6379` đã xác minh cả 3 node đều dùng `maxmemory-policy noeviction`: `.128`, `.129`, `.130`.
- Product cache đã được tách sang Redis Cache riêng `192.168.203.128:6380`, cấu hình `maxmemory` và `allkeys-lru`.
- Redis Cache `6380` đã xác minh `maxmemory = 134217728` và `maxmemory-policy = allkeys-lru`.
- Redis Persistence `6381` đã xác minh `appendonly = yes` và `save = 900 1 300 10 60 10000`.
- Không nên dùng chung một Redis `allkeys-lru` cho cả cache, session, cart và stream vì có thể làm Redis tự đẩy mất dữ liệu nghiệp vụ.

Backend connection:

```text
ASP.NET Core
-> Sentinel cluster
-> get-master-addr-by-name mymaster
-> Redis master hiện tại
```

## 10. Dashboard Infrastructure

Trang `/redis/infrastructure` hiển thị:

- Current master.
- Role hiện tại.
- Connected replicas.
- Redis version.
- OS.
- RDB status.
- AOF status.
- Memory policy của current master.
- Maxmemory của current master.
- Memory peak.
- Topology 3 Redis node.
- Memory policy của từng Redis HA node.
- Sentinel quorum.
- Persistence test.
- Cache endpoint `192.168.203.128:6380`.
- Cache maxmemory `134217728` bytes.
- Cache policy `allkeys-lru`.
- Persistence endpoint `192.168.203.128:6381`.
- Persistence appendonly `yes`.
- Persistence RDB save rules `900 1 300 10 60 10000`.
- HA checklist.

## 11. Kiểm Thử Đã Thực Hiện

Build:

```text
dotnet build
npm run build
```

Kết quả gần nhất:

- Backend build: pass, 0 warning.
- Shop frontend build: pass.
- Redis Dashboard build: pass.

Kiểm thử cache danh sách:

```text
DELETE /api/products/cache
GET /api/products?trackMetrics=false
-> X-Cache-Mode: OBSERVE_ONLY

GET /api/products
-> X-Cache-Rebuild: LOCK_OWNER
-> X-Data-Source: MYSQL

GET /api/products
-> X-Data-Source: REDIS
```

Kiểm thử cache chi tiết:

```text
GET /api/products/1
-> X-Cache-Rebuild: LOCK_OWNER
-> X-Data-Source: MYSQL

GET /api/products/1
-> X-Data-Source: REDIS
-> ttl khoảng 299s
```

Kiểm thử Pub/Sub:

```text
POST /api/redis/pubsub/publish
-> redisSubscribers: 1
-> sseClients: 1 hoặc nhiều hơn nếu mở nhiều tab
```

Kiểm thử backend sau chỉnh sửa đơn hàng/stream:

```text
dotnet build .\backend\RedisDemo.Api.csproj -o .\backend\bin\verify-build
-> Build succeeded, 0 warning, 0 error
```

Kiểm thử cấu hình runtime Redis:

```text
Current master:
192.168.203.130:6379

Redis HA policies:
192.168.203.128:6379 -> noeviction
192.168.203.129:6379 -> noeviction
192.168.203.130:6379 -> noeviction

Redis Cache:
Endpoint  -> 192.168.203.128:6380
Maxmemory -> 134217728
Policy    -> allkeys-lru

Redis Persistence:
Endpoint   -> 192.168.203.128:6381
Appendonly -> yes
Save       -> 900 1 300 10 60 10000
```

Kiểm thử Dashboard observe-only:

```text
BeforeHit  : 5984
ObserveHit : 5984
ShopHit    : 5985
```

## 12. Cách Chạy Hệ Thống

Backend:

```bash
cd backend
dotnet run --launch-profile http
```

Shop frontend:

```bash
cd frontend
npm install
npm run dev -- --host 127.0.0.1 --port 5173
```

Redis dashboard:

```bash
cd redis-dashboard
npm install
npm run dev
```

MySQL bằng Docker Compose nếu cần:

```bash
cd database
docker compose up -d
```

## 13. Tài Khoản Demo

User:

```text
Username: user01
Password: 123456
```

Admin:

```text
Username: admin
Password: admin
```

## 14. Trạng Thái Hoàn Thành

Đã hoàn thành:

- MySQL schema và seed data.
- Backend C# thật.
- Shop React.
- Redis Dashboard React.
- Cache-aside danh sách sản phẩm.
- Cache-aside chi tiết từng sản phẩm.
- Event-based cache invalidation.
- Chống cache stampede bằng Redis lock.
- Tạo đơn trừ tồn kho thật trong MySQL.
- Session store.
- Cart bằng Redis Hash.
- Rate limiting theo IP + username.
- Sorted Set ranking.
- Sorted Set ranking được cập nhật trong Stream worker sau khi xử lý đơn.
- Idempotency cho ranking bằng `ranking:processed-orders` và Lua script.
- Streams + consumer group + worker + claim lại pending message bằng `XPENDING`/`XCLAIM`.
- Pub/Sub realtime cho Dashboard và Shop.
- Persistence test.
- Redis Cache riêng port `6380` cho product cache.
- Redis Persistence Test riêng port `6381`, không Sentinel/replica.
- Redis Replication.
- Redis Sentinel.
- Backend HA connection qua Sentinel.
- Dashboard HA thật.

Còn có thể nâng cao thêm:

- Hiển thị riêng bảng hot product view từ `ranking:product:views`.
- Hiển thị lịch sử failover Sentinel.
- Chuyển password Redis/MySQL ra environment variable hoặc user-secrets.
- Nếu cần đúng chuẩn production hơn nữa, thêm transactional outbox cho luồng MySQL order -> Redis Stream.

## 15. Đánh Giá

Hệ thống hiện đã đủ để trình bày cả hai lớp:

- Redis dùng trong nghiệp vụ ứng dụng: cache, session, cart, rate limit, ranking, stream, pub/sub.
- Redis server trên Ubuntu theo hướng HA: replication, sentinel, failover, backend tự tìm master.

Phần cache hiện đã vượt mức cache-aside cơ bản vì có:

- cache danh sách,
- cache chi tiết từng sản phẩm,
- hot/cold product cache dựa trên cache từng sản phẩm, view counter `ranking:product:views` và LRU eviction ở Redis Cache `6380`,
- lock chống stampede,
- invalidation theo sự kiện tạo đơn,
- dashboard observe-only không làm nhiễu metric.



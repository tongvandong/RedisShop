# Redis Demo API

ASP.NET Core Web API ket noi MySQL that cho frontend.

## Chay API

Dam bao MySQL container dang chay:

```bash
cd ../database
docker compose up -d
```

Chay backend:

```bash
cd ../backend
dotnet run --urls http://127.0.0.1:5000
```

Swagger:

```text
http://127.0.0.1:5000/swagger
```

Health check:

```text
http://127.0.0.1:5000/api/health
```

## Tai khoan demo

```text
Username: user01
Password: 123456
```

## API da lam

- `POST /api/auth/login`
- `GET /api/products`
- `DELETE /api/products/cache`
- `GET /api/users/{userId}/cart`
- `PUT /api/users/{userId}/cart/items/{productId}`
- `DELETE /api/users/{userId}/cart`
- `GET /api/orders`
- `POST /api/orders`
- `GET /api/health`
- `GET /api/redis/ping`
- `GET /api/redis/overview`
- `GET /api/redis/ranking/products`
- `GET /api/redis/streams/orders`
- `POST /api/redis/pubsub/publish`

## Redis modules da trien khai

- Cache: `cache:products`, co HIT/MISS va TTL.
- Product cache dung Redis rieng `192.168.203.128:6380`.
- Session: login tao `session:<sessionId>` trong Redis.
- Cart: Redis Hash `cart:<userId>`.
- Rate limit: `rate:login:ip:<ip>:user:<username>`.
- Ranking: Redis Sorted Set `ranking:products`, co idempotency bang `ranking:processed-orders`.
- Streams: order ghi `XADD stream:orders`.
- Worker: `OrderStreamWorker` doc stream bang consumer group, claim pending message bang `XPENDING`/`XCLAIM`, cap nhat order thanh `processed`.
- Pub/Sub: publish message qua `/api/redis/pubsub/publish`.
- Persistence test dung Redis standalone `192.168.203.128:6381`.
- Dashboard API: `/api/redis/overview`, `/api/redis/ranking/products`, `/api/redis/streams/orders`.

## Ghi chu

Backend hien dang ket noi MySQL bang connection string trong `appsettings.json`.

Order tao tu frontend duoc ghi that vao MySQL:

- `orders`
- `order_items`

Sau khi ghi MySQL thanh cong, backend cap nhat Redis Ranking, ghi Redis Streams va worker xu ly stream.

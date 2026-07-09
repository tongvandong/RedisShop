# Redis Dashboard Frontend

Frontend React/Vite riêng cho 3 trang Redis, chạy ở cổng `5174`.

## Chạy project

```bash
npm install
npm run dev
```

Dev server:

```text
http://127.0.0.1:5174
```

Backend API mặc định:

```text
http://127.0.0.1:5000
```

Nếu cần đổi backend URL:

```text
VITE_API_BASE_URL=http://127.0.0.1:5000
```

## Trang Redis

- `/redis/overview`
- `/redis/streams`
- `/redis/infrastructure`

Các trang này gọi API Redis thật từ backend C#:

- `GET /api/redis/overview`
- `GET /api/redis/ranking/products`
- `GET /api/redis/streams/orders`
- `POST /api/redis/pubsub/publish`
- `DELETE /api/products/cache`

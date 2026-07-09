# Redis Shop Frontend

Frontend React/Vite cho phần shop, chạy riêng ở cổng `5173`.

## Chạy project

```bash
npm install
npm run dev
```

Dev server:

```text
http://127.0.0.1:5173
```

Backend API mặc định:

```text
http://127.0.0.1:5000
```

Nếu cần đổi backend URL:

```text
VITE_API_BASE_URL=http://127.0.0.1:5000
```

## Trang trong shop

Login là cửa vào ban đầu, không nằm ngang hàng với menu bên trong shop:

- `/shop/login`
- `/shop/products`
- `/shop/cart`
- `/shop/orders`

Luồng chính:

```text
/ hoặc /shop -> /shop/login
Đăng nhập -> /shop/products
Products / Cart / Orders -> yêu cầu đã đăng nhập
Đăng xuất -> /shop/login
```

Redis dashboard đã được tách sang project `redis-dashboard`, chạy ở:

```text
http://127.0.0.1:5174/redis/overview
```

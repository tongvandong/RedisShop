# Data Dictionary

## users

Luu tai khoan nguoi dung va admin demo.

| Column | Type | Meaning |
| --- | --- | --- |
| `id` | `BIGINT UNSIGNED` | Khoa chinh. Seed user chinh la `101`. |
| `username` | `VARCHAR(50)` | Ten dang nhap, duy nhat. |
| `email` | `VARCHAR(120)` | Email, duy nhat. |
| `password_hash` | `VARCHAR(255)` | Mat khau da hash trong ban that; seed dang la placeholder. |
| `full_name` | `VARCHAR(120)` | Ten hien thi. |
| `role` | `customer/admin` | Phan quyen don gian. |
| `is_active` | `BOOLEAN` | Trang thai tai khoan. |
| `created_at`, `updated_at` | `DATETIME(6)` | Thoi diem tao/cap nhat. |

## products

Luu danh muc san pham lau dai. Backend cache danh sach nay sang Redis key `cache:products`.

| Column | Type | Meaning |
| --- | --- | --- |
| `id` | `BIGINT UNSIGNED` | Khoa chinh. |
| `sku` | `VARCHAR(40)` | Ma san pham, duy nhat. |
| `name` | `VARCHAR(160)` | Ten san pham. |
| `description` | `VARCHAR(1000)` | Mo ta ngan. |
| `category` | `VARCHAR(80)` | Nhom san pham. |
| `price` | `DECIMAL(12,2)` | Gia ban VND. |
| `stock_quantity` | `INT UNSIGNED` | Ton kho demo. |
| `is_active` | `BOOLEAN` | Co hien tren shop hay khong. |
| `created_at`, `updated_at` | `DATETIME(6)` | Thoi diem tao/cap nhat. |

## orders

Luu don hang ben vung. Sau khi tao order, backend ghi them event vao Redis Streams `stream:orders`.

| Column | Type | Meaning |
| --- | --- | --- |
| `id` | `BIGINT UNSIGNED` | Khoa chinh. Seed bat dau tu `1001`. |
| `order_code` | `VARCHAR(40)` | Ma don hang, duy nhat. |
| `user_id` | `BIGINT UNSIGNED` | FK den `users.id`. |
| `status` | `ENUM` | `queued`, `pending`, `processed`, `completed`, `cancelled`, `failed`. |
| `total_amount` | `DECIMAL(12,2)` | Tong tien don hang. |
| `note` | `VARCHAR(500)` | Ghi chu demo. |
| `created_at`, `updated_at` | `DATETIME(6)` | Thoi diem tao/cap nhat. |

## order_items

Luu chi tiet tung san pham trong don hang.

| Column | Type | Meaning |
| --- | --- | --- |
| `id` | `BIGINT UNSIGNED` | Khoa chinh. |
| `order_id` | `BIGINT UNSIGNED` | FK den `orders.id`. |
| `product_id` | `BIGINT UNSIGNED` | FK den `products.id`. |
| `product_name_snapshot` | `VARCHAR(160)` | Ten san pham tai thoi diem dat hang. |
| `unit_price` | `DECIMAL(12,2)` | Gia tai thoi diem dat hang. |
| `quantity` | `INT UNSIGNED` | So luong, lon hon 0. |
| `line_total` | `DECIMAL(12,2)` | Cot generated: `unit_price * quantity`. |
| `created_at` | `DATETIME(6)` | Thoi diem tao dong. |

## Views

| View | Purpose |
| --- | --- |
| `v_order_summary` | Danh sach don hang kem username, so dong item, tong so luong. |
| `v_product_sales_ranking` | Ranking san pham dua tren order `processed/completed`; co the dung seed Redis Sorted Set ban dau. |

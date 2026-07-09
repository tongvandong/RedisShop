USE redis_shop_demo;

INSERT INTO users (id, username, email, password_hash, full_name, role, is_active)
VALUES
  (101, 'user01', 'user01@example.com', 'demo-password-123456', 'Nguyen Van Demo', 'customer', TRUE),
  (102, 'demo02', 'demo02@example.com', 'demo-password-123456', 'Tran Thi Redis', 'customer', TRUE),
  (103, 'guest03', 'guest03@example.com', 'demo-password-123456', 'Le Van Guest', 'customer', TRUE),
  (900, 'admin', 'admin@example.com', 'demo-password-admin', 'System Admin', 'admin', TRUE)
ON DUPLICATE KEY UPDATE
  email = VALUES(email),
  password_hash = VALUES(password_hash),
  full_name = VALUES(full_name),
  role = VALUES(role),
  is_active = VALUES(is_active);

INSERT INTO products (id, sku, name, description, category, price, stock_quantity, is_active)
VALUES
  (
    1,
    'KB-AK75',
    'Bàn phím cơ AK-75',
    'Bàn phím cơ 75%, switch êm, phù hợp demo sản phẩm bán chạy.',
    'Phụ kiện',
    890000,
    42,
    TRUE
  ),
  (
    2,
    'MS-M2-WL',
    'Chuột không dây M2',
    'Chuột không dây gọn nhẹ, pin lâu, phù hợp văn phòng.',
    'Phụ kiện',
    420000,
    85,
    TRUE
  ),
  (
    3,
    'HP-STUDIO-LITE',
    'Tai nghe Studio Lite',
    'Tai nghe over-ear cho học tập, họp trực tuyến và giải trí.',
    'Âm thanh',
    1250000,
    28,
    TRUE
  ),
  (
    4,
    'MN-24-FHD',
    'Màn hình 24 inch FHD',
    'Màn hình 24 inch Full HD, tấm nền IPS, tần số quét 75Hz.',
    'Màn hình',
    2890000,
    15,
    TRUE
  )
ON DUPLICATE KEY UPDATE
  sku = VALUES(sku),
  name = VALUES(name),
  description = VALUES(description),
  category = VALUES(category),
  price = VALUES(price),
  stock_quantity = VALUES(stock_quantity),
  is_active = VALUES(is_active);

INSERT INTO orders (id, order_code, user_id, status, total_amount, note, created_at)
VALUES
  (1001, 'ORD-1001', 101, 'processed', 3030000, 'Seed order da xu ly', '2026-07-07 09:35:00.000000'),
  (1002, 'ORD-1002', 102, 'pending', 420000, 'Seed order dang xu ly', '2026-07-07 09:40:00.000000'),
  (1003, 'ORD-1003', 101, 'queued', 2890000, 'Seed order cho worker xu ly', '2026-07-07 09:42:00.000000')
ON DUPLICATE KEY UPDATE
  user_id = VALUES(user_id),
  status = VALUES(status),
  total_amount = VALUES(total_amount),
  note = VALUES(note),
  created_at = VALUES(created_at);

INSERT INTO order_items (order_id, product_id, product_name_snapshot, unit_price, quantity)
VALUES
  (1001, 1, 'Bàn phím cơ AK-75', 890000, 2),
  (1001, 3, 'Tai nghe Studio Lite', 1250000, 1),
  (1002, 2, 'Chuột không dây M2', 420000, 1),
  (1003, 4, 'Màn hình 24 inch FHD', 2890000, 1)
ON DUPLICATE KEY UPDATE
  product_name_snapshot = VALUES(product_name_snapshot),
  unit_price = VALUES(unit_price),
  quantity = VALUES(quantity);

ALTER TABLE users AUTO_INCREMENT = 901;
ALTER TABLE products AUTO_INCREMENT = 5;
ALTER TABLE orders AUTO_INCREMENT = 1004;

USE redis_shop_demo;

UPDATE products
SET
  name = 'Bàn phím cơ AK-75',
  description = 'Bàn phím cơ 75%, switch êm, phù hợp demo sản phẩm bán chạy.',
  category = 'Phụ kiện'
WHERE id = 1;

UPDATE products
SET
  name = 'Chuột không dây M2',
  description = 'Chuột không dây gọn nhẹ, pin lâu, phù hợp văn phòng.',
  category = 'Phụ kiện'
WHERE id = 2;

UPDATE products
SET
  name = 'Tai nghe Studio Lite',
  description = 'Tai nghe over-ear cho học tập, họp trực tuyến và giải trí.',
  category = 'Âm thanh'
WHERE id = 3;

UPDATE products
SET
  name = 'Màn hình 24 inch FHD',
  description = 'Màn hình 24 inch Full HD, tấm nền IPS, tần số quét 75Hz.',
  category = 'Màn hình'
WHERE id = 4;

UPDATE order_items oi
JOIN products p ON p.id = oi.product_id
SET oi.product_name_snapshot = p.name;

USE redis_shop_demo;

SELECT 'users' AS table_name, COUNT(*) AS row_count FROM users
UNION ALL
SELECT 'products', COUNT(*) FROM products
UNION ALL
SELECT 'orders', COUNT(*) FROM orders
UNION ALL
SELECT 'order_items', COUNT(*) FROM order_items;

SELECT
  order_code,
  username,
  status,
  total_amount,
  item_count,
  total_quantity
FROM v_order_summary
ORDER BY id;

SELECT
  product_id,
  name,
  category,
  sales_count,
  revenue
FROM v_product_sales_ranking
ORDER BY sales_count DESC, product_id ASC;

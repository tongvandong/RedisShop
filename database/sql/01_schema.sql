CREATE DATABASE IF NOT EXISTS redis_shop_demo
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE redis_shop_demo;

CREATE TABLE IF NOT EXISTS users (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  username VARCHAR(50) NOT NULL,
  email VARCHAR(120) NOT NULL,
  password_hash VARCHAR(255) NOT NULL,
  full_name VARCHAR(120) NOT NULL,
  role ENUM('customer', 'admin') NOT NULL DEFAULT 'customer',
  is_active BOOLEAN NOT NULL DEFAULT TRUE,
  created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
    ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (id),
  UNIQUE KEY ux_users_username (username),
  UNIQUE KEY ux_users_email (email),
  KEY ix_users_role_active (role, is_active)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS products (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  sku VARCHAR(40) NOT NULL,
  name VARCHAR(160) NOT NULL,
  description VARCHAR(1000) NULL,
  category VARCHAR(80) NOT NULL,
  price DECIMAL(12,2) NOT NULL,
  stock_quantity INT UNSIGNED NOT NULL DEFAULT 0,
  is_active BOOLEAN NOT NULL DEFAULT TRUE,
  created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
    ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (id),
  UNIQUE KEY ux_products_sku (sku),
  KEY ix_products_category_active (category, is_active),
  KEY ix_products_active_name (is_active, name),
  CONSTRAINT ck_products_price_non_negative CHECK (price >= 0),
  CONSTRAINT ck_products_stock_non_negative CHECK (stock_quantity >= 0)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS orders (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  order_code VARCHAR(40) NOT NULL,
  user_id BIGINT UNSIGNED NOT NULL,
  status ENUM('queued', 'pending', 'processed', 'completed', 'cancelled', 'failed')
    NOT NULL DEFAULT 'queued',
  total_amount DECIMAL(12,2) NOT NULL DEFAULT 0,
  note VARCHAR(500) NULL,
  created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
    ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (id),
  UNIQUE KEY ux_orders_order_code (order_code),
  KEY ix_orders_user_created (user_id, created_at DESC),
  KEY ix_orders_status_created (status, created_at DESC),
  CONSTRAINT fk_orders_users
    FOREIGN KEY (user_id) REFERENCES users(id)
    ON UPDATE CASCADE
    ON DELETE RESTRICT,
  CONSTRAINT ck_orders_total_non_negative CHECK (total_amount >= 0)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS order_items (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  order_id BIGINT UNSIGNED NOT NULL,
  product_id BIGINT UNSIGNED NOT NULL,
  product_name_snapshot VARCHAR(160) NOT NULL,
  unit_price DECIMAL(12,2) NOT NULL,
  quantity INT UNSIGNED NOT NULL,
  line_total DECIMAL(12,2) GENERATED ALWAYS AS (unit_price * quantity) STORED,
  created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (id),
  UNIQUE KEY ux_order_items_order_product (order_id, product_id),
  KEY ix_order_items_product_id (product_id),
  CONSTRAINT fk_order_items_orders
    FOREIGN KEY (order_id) REFERENCES orders(id)
    ON UPDATE CASCADE
    ON DELETE CASCADE,
  CONSTRAINT fk_order_items_products
    FOREIGN KEY (product_id) REFERENCES products(id)
    ON UPDATE CASCADE
    ON DELETE RESTRICT,
  CONSTRAINT ck_order_items_unit_price_non_negative CHECK (unit_price >= 0),
  CONSTRAINT ck_order_items_quantity_positive CHECK (quantity > 0)
) ENGINE=InnoDB;

CREATE OR REPLACE VIEW v_order_summary AS
SELECT
  o.id,
  o.order_code,
  o.status,
  o.user_id,
  u.username,
  o.total_amount,
  COUNT(oi.id) AS item_count,
  COALESCE(SUM(oi.quantity), 0) AS total_quantity,
  o.created_at,
  o.updated_at
FROM orders o
JOIN users u ON u.id = o.user_id
LEFT JOIN order_items oi ON oi.order_id = o.id
GROUP BY
  o.id,
  o.order_code,
  o.status,
  o.user_id,
  u.username,
  o.total_amount,
  o.created_at,
  o.updated_at;

CREATE OR REPLACE VIEW v_product_sales_ranking AS
SELECT
  p.id AS product_id,
  p.sku,
  p.name,
  p.category,
  p.price,
  p.stock_quantity,
  COALESCE(SUM(CASE
    WHEN o.status IN ('processed', 'completed') THEN oi.quantity
    ELSE 0
  END), 0) AS sales_count,
  COALESCE(SUM(CASE
    WHEN o.status IN ('processed', 'completed') THEN oi.line_total
    ELSE 0
  END), 0) AS revenue
FROM products p
LEFT JOIN order_items oi ON oi.product_id = p.id
LEFT JOIN orders o ON o.id = oi.order_id
WHERE p.is_active = TRUE
GROUP BY
  p.id,
  p.sku,
  p.name,
  p.category,
  p.price,
  p.stock_quantity;

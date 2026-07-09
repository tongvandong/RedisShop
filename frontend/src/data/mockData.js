export const products = [
  {
    id: 1,
    name: 'Bàn phím cơ AK-75',
    category: 'Phụ kiện',
    price: 890000,
    stock: 42,
    cacheSource: 'REDIS',
    ttl: 52,
    score: 184,
  },
  {
    id: 2,
    name: 'Chuột không dây M2',
    category: 'Phụ kiện',
    price: 420000,
    stock: 85,
    cacheSource: 'MYSQL',
    ttl: 0,
    score: 156,
  },
  {
    id: 3,
    name: 'Tai nghe Studio Lite',
    category: 'Âm thanh',
    price: 1250000,
    stock: 28,
    cacheSource: 'REDIS',
    ttl: 48,
    score: 132,
  },
  {
    id: 4,
    name: 'Màn hình 24 inch FHD',
    category: 'Màn hình',
    price: 2890000,
    stock: 15,
    cacheSource: 'REDIS',
    ttl: 41,
    score: 96,
  },
]

export const initialCart = [
  { productId: 1, quantity: 2 },
  { productId: 3, quantity: 1 },
]

export const orders = [
  {
    id: 1001,
    total: 3030000,
    status: 'Processed',
    streamId: '1719901024-0',
    worker: 'Worker-1',
  },
  {
    id: 1002,
    total: 420000,
    status: 'Pending',
    streamId: '1719901045-0',
    worker: 'Worker-2',
  },
  {
    id: 1003,
    total: 2890000,
    status: 'Queued',
    streamId: '1719901058-0',
    worker: 'Waiting',
  },
]

export const sessions = [
  { user: 'user01', id: 'session:a8f92x', ttl: 1720, status: 'Active' },
  { user: 'demo02', id: 'session:b44e1q', ttl: 880, status: 'Active' },
  { user: 'guest03', id: 'session:c10p9k', ttl: 95, status: 'Expiring' },
]

export const rateLimits = [
  { client: '192.168.100.1', endpoint: '/api/auth/login', count: 2, limit: 5, ttl: 44 },
  { client: '192.168.100.1', endpoint: '/api/products', count: 17, limit: 60, ttl: 51 },
  { client: '10.0.2.15', endpoint: '/api/cart', count: 6, limit: 10, ttl: 12 },
]

export const streamMessages = [
  { id: '1719901024-0', orderId: 1001, consumer: 'Worker-1', state: 'ACK' },
  { id: '1719901045-0', orderId: 1002, consumer: 'Worker-2', state: 'PENDING' },
  { id: '1719901058-0', orderId: 1003, consumer: '-', state: 'QUEUED' },
]

export const notifications = [
  { channel: 'notifications', message: 'Đơn hàng mới #1003', time: '09:42:11' },
  { channel: 'flash-sale', message: 'Flash sale phụ kiện 50%', time: '09:39:44' },
  { channel: 'inventory', message: 'Màn hình 24 inch sắp hết hàng', time: '09:33:02' },
]

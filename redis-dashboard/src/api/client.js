export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://127.0.0.1:5000'

async function request(path, options = {}) {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    cache: 'no-store',
    headers: {
      'Content-Type': 'application/json',
      'Cache-Control': 'no-cache',
      ...(options.headers ?? {}),
    },
    ...options,
  })

  if (!response.ok) {
    let message = `Yêu cầu thất bại với mã ${response.status}`

    try {
      const errorBody = await response.json()
      message = errorBody.message ?? message
    } catch {
      // Backend không phải lúc nào cũng trả JSON khi có lỗi.
    }

    throw new Error(message)
  }

  if (response.status === 204) {
    return null
  }

  return response.json()
}

export function getRedisOverview() {
  return request('/api/redis/overview')
}

export function getRedisDetails() {
  return request('/api/redis/details')
}

export function getRedisInfrastructure() {
  return request('/api/redis/infrastructure')
}

export function preparePersistenceTest() {
  return request('/api/redis/persistence/prepare', {
    method: 'POST',
  })
}

export function checkPersistenceTest() {
  return request('/api/redis/persistence/check')
}

export function clearPersistenceTest() {
  return request('/api/redis/persistence/clear', {
    method: 'DELETE',
  })
}

export function getRedisRanking() {
  return request('/api/redis/ranking/products')
}

export function getRedisProductViewRanking() {
  return request('/api/redis/ranking/product-views')
}

export function getRedisOrderStream() {
  return request('/api/redis/streams/orders')
}

export function publishRedisMessage(channel, message) {
  return request('/api/redis/pubsub/publish', {
    method: 'POST',
    body: JSON.stringify({ channel, message }),
  })
}

export function clearProductsCache() {
  return request('/api/products/cache', {
    method: 'DELETE',
  })
}

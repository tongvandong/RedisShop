export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://127.0.0.1:5000'

function readSessionId() {
  const savedUser = localStorage.getItem('redis-demo-user')
  if (!savedUser) {
    return ''
  }

  try {
    return JSON.parse(savedUser).sessionId ?? ''
  } catch {
    return ''
  }
}

async function request(path, options = {}) {
  const sessionId = readSessionId()
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: {
      'Content-Type': 'application/json',
      ...(sessionId ? { 'X-Session-Id': sessionId } : {}),
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
      // Giữ thông báo mặc định khi backend không trả JSON.
    }
    throw new Error(message)
  }

  if (response.status === 204) {
    return null
  }

  return response.json()
}

export function login(username, password) {
  return request('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ username, password }),
  })
}

export function logout(sessionId) {
  return request('/api/auth/logout', {
    method: 'POST',
    body: JSON.stringify({ sessionId }),
  })
}

export function getProducts() {
  return request('/api/products')
}

export function getProduct(productId) {
  return request(`/api/products/${productId}`)
}

export function getOrders() {
  return request('/api/orders')
}

export function createOrder(userId, items) {
  return request('/api/orders', {
    method: 'POST',
    body: JSON.stringify({ userId, items }),
  })
}

export function getCart(userId) {
  return request(`/api/users/${userId}/cart`)
}

export function saveCartItem(userId, productId, quantity) {
  return request(`/api/users/${userId}/cart/items/${productId}`, {
    method: 'PUT',
    body: JSON.stringify({ productId, quantity }),
  })
}

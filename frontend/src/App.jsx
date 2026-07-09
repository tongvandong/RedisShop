import { useEffect, useMemo, useState } from 'react'
import { Navigate, Route, Routes, useNavigate } from 'react-router-dom'
import {
  API_BASE_URL,
  createOrder,
  getCart,
  getOrders,
  getProducts,
  login,
  logout as logoutSession,
  saveCartItem,
} from './api/client.js'
import { AppLayout } from './components/AppLayout.jsx'
import { CartPage } from './pages/shop/CartPage.jsx'
import { LoginPage } from './pages/shop/LoginPage.jsx'
import { OrdersPage } from './pages/shop/OrdersPage.jsx'
import { ProductDetailPage } from './pages/shop/ProductDetailPage.jsx'
import { ProductsPage } from './pages/shop/ProductsPage.jsx'
import './App.css'

function App() {
  const navigate = useNavigate()
  const [currentUser, setCurrentUser] = useState(() => {
    const savedUser = localStorage.getItem('redis-demo-user')
    return savedUser ? JSON.parse(savedUser) : null
  })
  const [products, setProducts] = useState([])
  const [orders, setOrders] = useState([])
  const [cart, setCart] = useState([])
  const [productsError, setProductsError] = useState('')
  const [cartError, setCartError] = useState('')
  const [ordersError, setOrdersError] = useState('')
  const [isLoadingProducts, setIsLoadingProducts] = useState(false)
  const [isCreatingOrder, setIsCreatingOrder] = useState(false)
  const [realtimeNotification, setRealtimeNotification] = useState(null)
  const [sseStatus, setSseStatus] = useState('disconnected')

  useEffect(() => {
    loadProducts()
    loadOrders()
  }, [])

  useEffect(() => {
    let isActive = true

    async function syncCart() {
      if (!currentUser) {
        setCart([])
        return
      }

      try {
        const nextCart = await getCart(currentUser.userId)
        if (isActive) {
          setCart(nextCart)
          setCartError('')
        }
      } catch (exception) {
        if (exception.message.includes('401')) {
          localStorage.removeItem('redis-demo-user')
          setCurrentUser(null)
          setCart([])
        } else if (isActive) {
          setCartError(exception.message)
        }
      }
    }

    syncCart()

    return () => {
      isActive = false
    }
  }, [currentUser])

  useEffect(() => {
    if (!currentUser) {
      setRealtimeNotification(null)
      setSseStatus('disconnected')
      return undefined
    }

    const events = new EventSource(`${API_BASE_URL}/api/redis/pubsub/notifications`)
    setSseStatus('connecting')

    events.addEventListener('ready', () => {
      setSseStatus('connected')
    })

    events.addEventListener('notification', (event) => {
      const payload = JSON.parse(event.data)
      setRealtimeNotification({
        channel: payload.channel,
        message: payload.message,
        time: payload.time,
      })
    })

    events.onerror = () => {
      setSseStatus('disconnected')
    }

    return () => events.close()
  }, [currentUser])

  const cartItems = useMemo(
    () =>
      cart
        .map((item) => ({
          ...item,
          product: products.find((product) => product.id === item.productId),
        }))
        .filter((item) => item.product),
    [cart, products],
  )

  const cartTotal = cartItems.reduce(
    (sum, item) => sum + item.product.price * item.quantity,
    0,
  )
  const cartQuantity = cart.reduce((sum, item) => sum + item.quantity, 0)

  async function addToCart(productId) {
    if (!currentUser) {
      return
    }

    const existing = cart.find((item) => item.productId === productId)
    const quantity = existing ? existing.quantity + 1 : 1
    const optimisticCart = existing
      ? cart.map((item) => (item.productId === productId ? { ...item, quantity } : item))
      : [...cart, { productId, quantity }]

    setCart(optimisticCart)
    setCartError('')

    try {
      setCart(await saveCartItem(currentUser.userId, productId, quantity))
      setCartError('')
    } catch (exception) {
      setCartError(exception.message)
    }
  }

  async function updateQuantity(productId, delta) {
    if (!currentUser) {
      return
    }

    const existing = cart.find((item) => item.productId === productId)
    const quantity = Math.max(0, (existing?.quantity ?? 0) + delta)
    setCart(await saveCartItem(currentUser.userId, productId, quantity))
  }

  function refreshProducts() {
    loadProducts()
  }

  async function logout() {
    const sessionId = currentUser?.sessionId
    if (sessionId) {
      try {
        await logoutSession(sessionId)
      } catch {
        // Vẫn xoá dữ liệu local nếu backend tạm thời mất kết nối.
      }
    }

    localStorage.removeItem('redis-demo-user')
    setCurrentUser(null)
    setCart([])
  }

  async function loadProducts() {
    setIsLoadingProducts(true)
    setProductsError('')

    try {
      const nextProducts = await getProducts()
      setProducts(nextProducts)
    } catch (exception) {
      setProductsError(exception.message)
    } finally {
      setIsLoadingProducts(false)
    }
  }

  async function loadOrders() {
    setOrdersError('')

    try {
      setOrders(await getOrders())
    } catch (exception) {
      setOrdersError(exception.message)
    }
  }

  async function loginWithBackend(username, password) {
    const user = await login(username, password)
    localStorage.setItem('redis-demo-user', JSON.stringify(user))
    setCurrentUser(user)
  }

  async function createOrderFromCart() {
    if (!currentUser) {
      setOrdersError('Bạn cần đăng nhập trước khi tạo đơn hàng.')
      return
    }

    if (cart.length === 0) {
      setOrdersError('Giỏ hàng đang trống.')
      return
    }

    setIsCreatingOrder(true)
    setOrdersError('')

    try {
      await createOrder(
        currentUser.userId,
        cart.map((item) => ({
          productId: item.productId,
          quantity: item.quantity,
        })),
      )
      setCart([])
      await loadOrders()
      await loadProducts()
      navigate('/shop/orders')
    } catch (exception) {
      setOrdersError(exception.message)
    } finally {
      setIsCreatingOrder(false)
    }
  }

  function requireLogin(page) {
    return currentUser ? page : <Navigate to="/shop/login" replace />
  }

  const appRoutes = (
    <Routes>
      <Route
        path="/shop/products"
        element={requireLogin(
          <ProductsPage
            products={products}
            onRefreshProducts={refreshProducts}
            onAddToCart={addToCart}
            isLoading={isLoadingProducts}
            error={productsError}
          />,
        )}
      />
      <Route
        path="/shop/products/:productId"
        element={requireLogin(
          <ProductDetailPage onAddToCart={addToCart} />,
        )}
      />
      <Route
        path="/shop/cart"
        element={requireLogin(
          <CartPage
            items={cartItems}
            total={cartTotal}
            onUpdateQuantity={updateQuantity}
            onCreateOrder={createOrderFromCart}
            isCreating={isCreatingOrder}
            error={cartError}
            orderError={ordersError}
          />,
        )}
      />
      <Route
        path="/shop/orders"
        element={requireLogin(
          <OrdersPage
            orders={orders}
            error={ordersError}
          />,
        )}
      />

      <Route path="/products" element={<Navigate to="/shop/products" replace />} />
      <Route path="/cart" element={<Navigate to="/shop/cart" replace />} />
      <Route path="/orders" element={<Navigate to="/shop/orders" replace />} />
      <Route path="/redis/*" element={<Navigate to="/shop/products" replace />} />
      <Route path="/redis-dashboard/*" element={<Navigate to="/shop/products" replace />} />
    </Routes>
  )

  return (
    <Routes>
      <Route path="/" element={<Navigate to="/shop/login" replace />} />
      <Route path="/shop" element={<Navigate to="/shop/login" replace />} />
      <Route path="/login" element={<Navigate to="/shop/login" replace />} />
      <Route
        path="/shop/login"
        element={
          currentUser ? (
            <Navigate to="/shop/products" replace />
          ) : (
            <LoginPage currentUser={currentUser?.username} onLogin={loginWithBackend} />
          )
        }
      />
      <Route
        path="/*"
        element={
          <AppLayout currentUser={currentUser?.username} cartCount={cartQuantity} onLogout={logout}>
            {realtimeNotification && (
              <div className="shop-notification" role="status">
                <strong>Thông báo realtime</strong>
                <span>{realtimeNotification.message}</span>
                <time>{realtimeNotification.time}</time>
              </div>
            )}
            {currentUser && sseStatus !== 'connected' && (
              <div className="shop-notification muted" role="status">
                <strong>Pub/Sub</strong>
                <span>Đang kết nối kênh thông báo realtime...</span>
              </div>
            )}
            {appRoutes}
          </AppLayout>
        }
      />
    </Routes>
  )
}

export default App

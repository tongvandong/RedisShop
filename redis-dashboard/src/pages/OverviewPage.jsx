import {
  Bell,
  Clock3,
  Database,
  Eraser,
  Gauge,
  KeyRound,
  LockKeyhole,
  Package,
  RefreshCcw,
  ShieldAlert,
  ShoppingCart,
  Timer,
  Trophy,
  Zap,
} from 'lucide-react'
import { useState } from 'react'
import { EmptyState, MetricCard, Panel, StatusPill } from '../components/Ui.jsx'

function formatVnd(value) {
  return new Intl.NumberFormat('vi-VN', {
    style: 'currency',
    currency: 'VND',
    maximumFractionDigits: 0,
  }).format(value ?? 0)
}

function hitRate(cache) {
  const total = (cache.hit ?? 0) + (cache.miss ?? 0)
  return total === 0 ? '0%' : `${Math.round(((cache.hit ?? 0) / total) * 100)}%`
}

function duration(value) {
  return `${value ?? 0} ms`
}

export function OverviewPage({
  overview,
  details,
  ranking,
  viewRanking,
  isLoading,
  error,
  lastNotification,
  notifications,
  sseStatus,
  publishResult,
  onRefreshProducts,
  onClearCache,
  onPublish,
}) {
  const [publishMessage, setPublishMessage] = useState('Flash Sale 50% bắt đầu lúc 20:00')
  const [cacheAction, setCacheAction] = useState('')
  const [isPublishing, setIsPublishing] = useState(false)
  const cache = overview?.cache ?? {}
  const sessions = details?.sessions ?? []
  const carts = details?.carts ?? []
  const rateLimits = details?.rateLimits ?? []
  const productSource = cache.lastSource ?? 'UNKNOWN'
  const cacheTone = productSource === 'REDIS' ? 'green' : productSource === 'MYSQL' ? 'amber' : 'blue'
  const speedupText = cache.speedup > 0 ? `${cache.speedup}x` : '--'

  async function submitPublish(event) {
    event.preventDefault()
    const message = publishMessage.trim()
    if (message) {
      setIsPublishing(true)
      try {
        await onPublish(message)
      } finally {
        setIsPublishing(false)
      }
    }
  }

  async function refreshCacheMetrics() {
    setCacheAction('refresh')
    try {
      await onRefreshProducts()
    } finally {
      setCacheAction('')
    }
  }

  async function clearCache() {
    setCacheAction('clear')
    try {
      await onClearCache()
    } finally {
      setCacheAction('')
    }
  }

  return (
    <section className="page-stack">
      {error && <p className="form-error">{error}</p>}

      <div className="metric-grid">
        <MetricCard icon={Database} label="Trạng thái Redis" value={overview?.online ? 'Online' : 'Offline'} sublabel={overview?.endpoint ?? 'Chưa nhận dữ liệu'} tone={overview?.online ? 'green' : 'red'} />
        <MetricCard icon={KeyRound} label="Tổng số key" value={overview?.totalKeys ?? 0} sublabel="Quét DB hiện tại" tone="blue" />
        <MetricCard icon={Gauge} label="Bộ nhớ" value={overview?.usedMemoryHuman ?? '0B'} sublabel={`${overview?.connectedClients ?? 0} client`} tone="violet" />
        <MetricCard icon={Clock3} label="Uptime" value={overview?.uptime ?? '00:00:00'} sublabel="Thời gian Redis chạy" tone="green" />
      </div>

      <div className="metric-grid metric-grid-compact">
        <MetricCard icon={Package} label="Cache HIT rate" value={hitRate(cache)} sublabel={`HIT ${cache.hit ?? 0} / MISS ${cache.miss ?? 0}`} tone={cacheTone} />
        <MetricCard icon={Timer} label="Lần tải gần nhất" value={duration(cache.lastDurationMs)} sublabel={`Nguồn: ${productSource}`} tone={cacheTone} />
        <MetricCard icon={Zap} label="Redis nhanh hơn" value={speedupText} sublabel={`Redis ${duration(cache.redisDurationMs)} / MySQL ${duration(cache.mySqlDurationMs)}`} tone={cache.speedup > 0 ? 'green' : 'blue'} />
        <MetricCard icon={ShieldAlert} label="Rate-limit keys" value={rateLimits.length} sublabel="Counter chống spam" tone="amber" />
      </div>

      <div className="content-split">
        <Panel
          icon={Package}
          title="Cache Monitor"
          description="Cache-aside cho danh sách sản phẩm: lần đầu lấy MySQL, lần sau lấy Redis cache."
          action={
            <div className="button-row">
              <button type="button" className="secondary-button" onClick={refreshCacheMetrics} disabled={isLoading || cacheAction !== ''}>
                <RefreshCcw size={16} />
                {cacheAction === 'refresh' ? 'Đang tải...' : 'Tải lại metric'}
              </button>
              <button type="button" className="danger-button" onClick={clearCache} disabled={cacheAction !== ''}>
                <Eraser size={16} />
                {cacheAction === 'clear' ? 'Đang xóa...' : 'Xóa cache'}
              </button>
            </div>
          }
        >
          <div className="cache-summary">
            <StatusPill tone={cacheTone} label={productSource} />
            <span>Key: {cache.key ?? 'cache:products'}</span>
            <span>TTL: {cache.ttl ?? 0}s</span>
            <span>HIT rate: {hitRate(cache)}</span>
          </div>
          <div className="status-grid cache-timing-grid">
            <div><span>Last load</span><strong>{duration(cache.lastDurationMs)}</strong></div>
            <div><span>MySQL load</span><strong>{duration(cache.mySqlDurationMs)}</strong></div>
            <div><span>Redis cache load</span><strong>{duration(cache.redisDurationMs)}</strong></div>
            <div><span>Speedup</span><strong>{speedupText}</strong></div>
          </div>
        </Panel>

        <div className="ranking-stack">
          <Panel icon={Trophy} title="Ranking bán chạy" description="Redis Sorted Set ranking:products, tăng sau khi Worker xử lý order.">
            <div className="ranking-list">
              {ranking.length === 0 && <EmptyState title="Chưa có điểm bán hàng" description="Tạo đơn ở shop để backend tăng score trong Redis." />}
              {ranking.map((item, index) => (
                <div key={item.productId}><strong>#{index + 1}</strong><span>{item.name}</span><b>{item.score}</b></div>
              ))}
            </div>
          </Panel>

          <Panel icon={Trophy} title="Ranking lượt xem" description="Redis Sorted Set ranking:product:views, tăng khi mở trang chi tiết sản phẩm.">
            <div className="ranking-list">
              {(viewRanking ?? []).length === 0 && <EmptyState title="Chưa có lượt xem" description="Mở trang chi tiết sản phẩm ở shop để tăng view counter." />}
              {(viewRanking ?? []).map((item, index) => (
                <div key={item.productId}><strong>#{index + 1}</strong><span>{item.name}</span><b>{item.score}</b></div>
              ))}
            </div>
          </Panel>
        </div>
      </div>

      <div className="content-split equal-split">
        <Panel icon={LockKeyhole} title="Session Monitor" description="Các phiên đăng nhập đang được lưu trong Redis.">
          {sessions.length === 0 ? (
            <EmptyState title="Chưa có session" description="Đăng nhập ở shop để tạo key session:*." />
          ) : (
            <div className="table-wrap">
              <table>
                <thead><tr><th>Session key</th><th>User</th><th>Role</th><th>TTL</th><th>Created Time</th></tr></thead>
                <tbody>
                  {sessions.map((session) => (
                    <tr key={session.key}><td className="mono-cell">{session.key}</td><td>{session.username || session.userId}</td><td>{session.role}</td><td>{session.ttl}s</td><td>{session.createdAt || '--'}</td></tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Panel>

        <Panel icon={ShoppingCart} title="Cart Monitor" description="Redis Hash cart:*: field là productId, value là quantity.">
          {carts.length === 0 ? (
            <EmptyState title="Chưa có cart" description="Thêm sản phẩm vào giỏ ở shop để tạo key cart:*." />
          ) : (
            <div className="cart-monitor-list">
              {carts.map((cart) => (
                <article className="cart-monitor-card" key={cart.key}>
                  <div className="cart-monitor-head">
                    <div><strong className="mono-cell">{cart.key}</strong><span>User {cart.userId} · TTL {cart.ttl}s</span></div>
                    <div><b>{formatVnd(cart.totalAmount)}</b><span>{cart.itemCount} loại · {cart.totalQuantity} sản phẩm</span></div>
                  </div>
                  <div className="cart-monitor-items">
                    {(cart.items ?? []).map((item) => (
                      <div key={`${cart.key}-${item.productId}`}>
                        <span>{item.name}</span>
                        <b>{item.quantity} × {formatVnd(item.price)}</b>
                        <strong>{formatVnd(item.subtotal)}</strong>
                      </div>
                    ))}
                  </div>
                </article>
              ))}
            </div>
          )}
        </Panel>
      </div>

      <div className="content-split equal-split">
        <Panel icon={ShieldAlert} title="Rate Limit Monitor" description="Counter chống spam login, tự hết hạn theo TTL.">
          {rateLimits.length === 0 ? (
            <EmptyState title="Chưa có counter" description="Thử đăng nhập vài lần để tạo key rate:*." />
          ) : (
            <div className="table-wrap">
              <table>
                <thead><tr><th>Rate key</th><th>Client</th><th>Count</th><th>Limit</th><th>TTL</th><th>Status</th></tr></thead>
                <tbody>
                  {rateLimits.map((item) => (
                    <tr key={item.key}>
                      <td className="mono-cell">{item.key}</td><td>{item.client}</td><td>{item.count}</td><td>{item.limit}</td><td>{item.ttl}s</td>
                      <td><StatusPill tone={item.blocked ? 'red' : 'green'} label={item.blocked ? 'Blocked' : 'Allowed'} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Panel>

        <Panel
          icon={Bell}
          title="Pub/Sub Realtime"
          description="Publish vào Redis channel notifications; backend subscriber nhận rồi đẩy xuống React bằng SSE."
        >
          <form className="pubsub-form" onSubmit={submitPublish}>
            <label><span>Channel</span><input value="notifications" readOnly /></label>
            <label>
              <span>Nội dung thông báo</span>
              <input value={publishMessage} onChange={(event) => setPublishMessage(event.target.value)} placeholder="Flash Sale 50% bắt đầu lúc 20:00" />
            </label>
            <button type="submit" className="primary-button" disabled={isPublishing}>
              <Bell size={16} />
              {isPublishing ? 'Đang publish...' : 'Publish'}
            </button>
          </form>

          <div className="pubsub-flow">
            <span>React POST</span><b>ASP.NET Publisher</b><span>Redis channel</span><b>ASP.NET Subscriber</b><span>SSE EventSource</span>
          </div>

          <div className="pubsub-status-row">
            <StatusPill tone={sseStatus?.includes('connected') ? 'green' : 'red'} label={sseStatus ?? 'SSE unknown'} />
            <span>Redis subscribers: {publishResult?.redisSubscribers ?? '--'}</span>
            <span>SSE clients: {publishResult?.sseClients ?? '--'}</span>
          </div>

          <div className="notification-strip">
            <strong>{lastNotification.channel}</strong>
            <span>{lastNotification.message}</span>
            <time>{lastNotification.time}</time>
          </div>

          <div className="notification-history">
            {(notifications ?? []).length === 0 ? (
              <EmptyState title="Chưa có event realtime" description="Bấm Publish để thấy message chỉ xuất hiện khi quay về qua SSE." />
            ) : (
              notifications.map((notification, index) => (
                <article key={`${notification.time}-${index}`}>
                  <strong>{notification.message}</strong>
                  <span>{notification.channel} · {notification.receivedBy ?? 'Subscriber'} · {notification.time}</span>
                </article>
              ))
            )}
          </div>
        </Panel>
      </div>
    </section>
  )
}

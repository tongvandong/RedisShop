import { LogOut, ShoppingBag, ShoppingCart } from 'lucide-react'
import { NavLink, useLocation, useNavigate } from 'react-router-dom'
import { navGroups } from '../config/navigation.jsx'
import { StatusPill } from './Ui.jsx'

export function AppLayout({ children, currentUser, cartCount, onLogout }) {
  return (
    <div className="app-shell">
      <Sidebar />
      <main className="main-panel">
        <Topbar currentUser={currentUser} cartCount={cartCount} onLogout={onLogout} />
        {children}
      </main>
    </div>
  )
}

function Sidebar() {
  return (
    <aside className="sidebar">
      <div className="brand">
        <span className="brand-mark">
          <ShoppingBag size={20} />
        </span>
        <div>
          <strong>Mini Shop</strong>
          <span>Cửa hàng demo</span>
        </div>
      </div>

      <nav className="nav-list" aria-label="Điều hướng shop">
        {navGroups.map((group) => (
          <section key={group.label}>
            <p className="nav-label">{group.label}</p>
            {group.items.map((item) => {
              const Icon = item.icon
              return (
                <NavLink key={item.to} to={item.to}>
                  <Icon size={18} />
                  <span>{item.label}</span>
                </NavLink>
              )
            })}
          </section>
        ))}
      </nav>
    </aside>
  )
}

function Topbar({ currentUser, cartCount, onLogout }) {
  const location = useLocation()
  const navigate = useNavigate()
  const active = navGroups
    .flatMap((group) => group.items)
    .find((item) => item.to === location.pathname)
  const title = location.pathname === '/shop/login' ? 'Đăng nhập' : active?.label ?? 'Shop'

  async function logout() {
    await onLogout()
    navigate('/shop/login', { replace: true })
  }

  return (
    <header className="topbar">
      <div>
        <p className="eyebrow">Cửa hàng trực tuyến</p>
        <h1>{title}</h1>
      </div>
      <div className="topbar-actions">
        {currentUser ? (
          <>
            <span className="user-chip">{currentUser}</span>
            <button className="cart-chip" type="button" onClick={() => navigate('/shop/cart')}>
              <ShoppingCart size={16} />
              {cartCount}
            </button>
            <button className="logout-button" type="button" onClick={logout}>
              <LogOut size={16} />
              Đăng xuất
            </button>
          </>
        ) : (
          <StatusPill tone="amber" label="Chưa đăng nhập" />
        )}
      </div>
    </header>
  )
}

import { Activity, Clock3, Database, Layers3, RefreshCcw, Server, Signal } from 'lucide-react'
import { NavLink, useLocation } from 'react-router-dom'
import { StatusPill } from './Ui.jsx'

const navItems = [
  { to: '/redis/overview', label: 'Tổng quan', icon: Activity },
  { to: '/redis/streams', label: 'Luồng xử lý', icon: Layers3 },
  { to: '/redis/infrastructure', label: 'Hạ tầng', icon: Server },
]

export function Layout({ children, online, endpoint, lastUpdated, refreshSeconds, onRefresh }) {
  const location = useLocation()
  const active = navItems.find((item) => item.to === location.pathname)

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <span className="brand-mark">
            <Database size={20} />
          </span>
          <div>
            <strong>Redis Dashboard</strong>
            <span>Cổng 5174</span>
          </div>
        </div>

        <nav className="nav-list" aria-label="Redis dashboard navigation">
          {navItems.map((item) => {
            const Icon = item.icon
            return (
              <NavLink key={item.to} to={item.to}>
                <Icon size={18} />
                <span>{item.label}</span>
              </NavLink>
            )
          })}
        </nav>
      </aside>

      <main className="main-panel">
        <header className="topbar">
          <div>
            <p className="eyebrow">Theo dõi Redis thật từ backend C#</p>
            <h1>{active?.label ?? 'Redis Dashboard'}</h1>
          </div>
          <div className="topbar-actions">
            <StatusPill tone={online ? 'green' : 'red'} label={online ? 'Redis trực tuyến' : 'Redis mất kết nối'} />
            <span className="endpoint-chip">
              <Signal size={16} />
              {endpoint || 'Chưa có endpoint'}
            </span>
            <span className="endpoint-chip">
              <Clock3 size={16} />
              {lastUpdated ? `Cập nhật ${lastUpdated}` : `Tự cập nhật ${refreshSeconds}s`}
            </span>
            <button type="button" className="icon-button" onClick={onRefresh} title="Tải lại dữ liệu">
              <RefreshCcw size={17} />
            </button>
          </div>
        </header>
        {children}
      </main>
    </div>
  )
}

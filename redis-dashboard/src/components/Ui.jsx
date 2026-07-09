export function MetricCard({ icon: Icon, label, value, sublabel, tone = 'blue' }) {
  return (
    <article className={`metric-card metric-card-${tone}`}>
      <span className="metric-icon">
        <Icon size={20} />
      </span>
      <div>
        <p>{label}</p>
        <strong>{value}</strong>
        {sublabel && <span>{sublabel}</span>}
      </div>
    </article>
  )
}

export function StatusPill({ tone = 'blue', label }) {
  return <span className={`status-pill status-${tone}`}>{label}</span>
}

export function EmptyState({ title, description }) {
  return (
    <div className="empty-state">
      <strong>{title}</strong>
      <p>{description}</p>
    </div>
  )
}

export function Panel({ title, description, icon: Icon, action, children }) {
  return (
    <section className="panel">
      <div className="section-heading">
        {Icon && <Icon size={22} />}
        <div>
          <h2>{title}</h2>
          {description && <p>{description}</p>}
        </div>
        {action && <div className="section-action">{action}</div>}
      </div>
      {children}
    </section>
  )
}

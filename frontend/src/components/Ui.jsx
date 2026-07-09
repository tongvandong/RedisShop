export function StatusPill({ tone, label }) {
  return <span className={`status-pill ${tone}`}>{label}</span>
}

export function MetricCard({ icon: Icon, label, value, sublabel, tone = 'neutral' }) {
  return (
    <article className="metric-card">
      <span className={`metric-icon ${tone}`}>
        <Icon size={18} />
      </span>
      <div>
        <p>{label}</p>
        <strong>{value}</strong>
        <span>{sublabel}</span>
      </div>
    </article>
  )
}

export function RedisKeyPreview({ icon: Icon, title, rows }) {
  return (
    <section className="panel key-preview">
      <div className="section-heading">
        <Icon size={22} />
        <div>
          <h2>{title}</h2>
          <p>Thông tin này sẽ lấy trực tiếp từ Redis Dashboard API.</p>
        </div>
      </div>
      <dl>
        {rows.map(([key, value]) => (
          <div key={key}>
            <dt>{key}</dt>
            <dd>{value}</dd>
          </div>
        ))}
      </dl>
    </section>
  )
}

export function DataTable({ title, columns, rows }) {
  return (
    <section className="panel table-panel">
      <h2>{title}</h2>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              {columns.map((column) => (
                <th key={column}>{column}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.join('-')}>
                {row.map((cell) => (
                  <td key={cell}>{cell}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}

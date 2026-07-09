import { CheckCircle2, Clock3, Layers3, ListTree, PlayCircle, ServerCog, TimerReset } from 'lucide-react'
import { EmptyState, MetricCard, Panel, StatusPill } from '../components/Ui.jsx'

function statusTone(status) {
  return status === 'Running' ? 'green' : status === 'Pending' ? 'amber' : 'blue'
}

export function StreamsPage({ overview, details, messages }) {
  const stream = details?.stream ?? {}
  const processedCount = messages.filter((message) => message.status === 'processed').length
  const pendingCount = stream.pendingCount ?? 0
  const workerStatus = stream.workerStatus ?? 'Idle'

  return (
    <section className="page-stack">
      <div className="metric-grid">
        <MetricCard icon={Layers3} label="Stream key" value={stream.key ?? 'stream:orders'} sublabel="Hàng đợi đơn hàng mới" tone="blue" />
        <MetricCard icon={ListTree} label="Tổng message" value={stream.length ?? overview?.streamLength ?? messages.length} sublabel="XLEN stream:orders" tone="green" />
        <MetricCard icon={TimerReset} label="Pending" value={pendingCount} sublabel="Message chưa ACK" tone={pendingCount > 0 ? 'amber' : 'green'} />
        <MetricCard icon={ServerCog} label="Worker" value={workerStatus} sublabel={stream.consumerGroup ?? 'order-workers'} tone={statusTone(workerStatus)} />
      </div>

      <Panel icon={PlayCircle} title="Pipeline xử lý đơn hàng" description="Luồng này chứng minh Redis Stream được dùng cho xử lý bất đồng bộ, còn dữ liệu đơn hàng vẫn lưu bền trong MySQL.">
        <div className="pipeline">
          <div>
            <strong>1</strong>
            <b>Khách đặt hàng</b>
            <span>Shop gọi POST /api/orders</span>
          </div>
          <i aria-hidden="true" />
          <div>
            <strong>2</strong>
            <b>Lưu nghiệp vụ</b>
            <span>Backend ghi Orders và OrderItems</span>
          </div>
          <i aria-hidden="true" />
          <div>
            <strong>3</strong>
            <b>Đẩy sự kiện</b>
            <span>XADD stream:orders</span>
          </div>
          <i aria-hidden="true" />
          <div>
            <strong>4</strong>
            <b>Worker xử lý</b>
            <span>Consumer group đọc message</span>
          </div>
          <i aria-hidden="true" />
          <div>
            <strong>5</strong>
            <b>ACK hoàn tất</b>
            <span>Đơn chuyển sang processed</span>
          </div>
        </div>
      </Panel>

      <div className="content-split equal-split">
        <Panel icon={ServerCog} title="Consumer Group" description="Thông tin được lấy từ XPENDING và XINFO GROUPS.">
          <div className="status-grid">
            <div>
              <span>Group</span>
              <strong>{stream.consumerGroup ?? 'order-workers'}</strong>
            </div>
            <div>
              <span>Consumers</span>
              <strong>{stream.consumerCount ?? 0}</strong>
            </div>
            <div>
              <span>Pending</span>
              <strong>{pendingCount}</strong>
            </div>
            <div>
              <span>Last delivered ID</span>
              <strong className="mono-cell">{stream.lastDeliveredId || '--'}</strong>
            </div>
          </div>
          <div className="health-line">
            <StatusPill tone={statusTone(workerStatus)} label={workerStatus} />
            <span>{pendingCount === 0 ? 'Worker đang xử lý kịp, không còn message pending.' : 'Còn message chờ ACK, cần quan sát worker.'}</span>
          </div>
        </Panel>

        <Panel icon={CheckCircle2} title="Kết quả xử lý" description="Theo dõi nhanh số message đã hoàn tất và trạng thái hàng đợi.">
          <div className="status-grid">
            <div>
              <span>Đã xử lý</span>
              <strong>{processedCount}</strong>
            </div>
            <div>
              <span>Trong stream</span>
              <strong>{messages.length}</strong>
            </div>
            <div>
              <span>Pending summary</span>
              <strong>{stream.pendingSummary || 'Pending: 0'}</strong>
            </div>
            <div>
              <span>Group summary</span>
              <strong>{stream.groupsSummary || '--'}</strong>
            </div>
          </div>
        </Panel>
      </div>

      <Panel icon={Clock3} title="Message mới nhất" description="Danh sách message gần nhất trong stream:orders.">
        {messages.length === 0 ? (
          <EmptyState title="Stream đang trống" description="Tạo đơn hàng ở shop để sinh message mới." />
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Stream ID</th>
                  <th>Mã đơn</th>
                  <th>Order ID</th>
                  <th>Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {messages.map((message) => (
                  <tr key={message.id}>
                    <td className="mono-cell">{message.id}</td>
                    <td>{message.orderCode}</td>
                    <td>{message.orderId}</td>
                    <td>
                      <StatusPill tone={message.status === 'processed' ? 'green' : 'amber'} label={message.status} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Panel>
    </section>
  )
}

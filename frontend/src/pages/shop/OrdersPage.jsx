import { Boxes } from 'lucide-react'
import { DataTable } from '../../components/Ui.jsx'
import { formatVnd } from '../../utils/format.js'

export function OrdersPage({ orders, error }) {
  return (
    <section className="page-stack">
      <div className="panel">
        <div className="section-heading">
          <Boxes size={22} />
          <div>
            <h2>Đơn hàng</h2>
            <p>Theo dõi lịch sử mua hàng và trạng thái xử lý đơn.</p>
          </div>
        </div>
        {error && <p className="form-error order-error">{error}</p>}
      </div>

      <DataTable
        title="Lịch sử đơn hàng"
        columns={['Mã đơn', 'Tổng tiền', 'Trạng thái', 'Số lượng', 'Ngày tạo']}
        rows={orders.map((order) => [
          order.orderCode ?? `#${order.id}`,
          formatVnd(order.totalAmount),
          order.status,
          order.totalQuantity ?? order.itemCount ?? 0,
          order.createdAt ? new Date(order.createdAt).toLocaleString('vi-VN') : '--',
        ])}
      />
    </section>
  )
}

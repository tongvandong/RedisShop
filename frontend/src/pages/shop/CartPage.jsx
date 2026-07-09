import { CheckCircle2, ShoppingCart } from 'lucide-react'
import { formatVnd } from '../../utils/format.js'

export function CartPage({
  items,
  total,
  onUpdateQuantity,
  onCreateOrder,
  isCreating,
  error,
  orderError,
}) {
  const isEmpty = items.length === 0

  return (
    <section className="page-grid two-columns">
      <div className="panel">
        <div className="section-heading">
          <ShoppingCart size={22} />
          <div>
            <h2>Giỏ hàng</h2>
            <p>Kiểm tra sản phẩm, điều chỉnh số lượng rồi đặt hàng.</p>
          </div>
        </div>

        {error && <p className="form-error">{error}</p>}

        {isEmpty ? (
          <p className="state-text">Giỏ hàng của bạn đang trống.</p>
        ) : (
          <div className="cart-list">
            {items.map((item) => (
              <article key={item.productId} className="cart-row">
                <div>
                  <h3>{item.product.name}</h3>
                  <p>{formatVnd(item.product.price)}</p>
                </div>
                <div className="quantity-control">
                  <button type="button" onClick={() => onUpdateQuantity(item.productId, -1)}>
                    -
                  </button>
                  <strong>{item.quantity}</strong>
                  <button type="button" onClick={() => onUpdateQuantity(item.productId, 1)}>
                    +
                  </button>
                </div>
              </article>
            ))}
          </div>
        )}
      </div>

      <aside className="panel cart-summary">
        <div className="section-heading">
          <ShoppingCart size={22} />
          <div>
            <h2>Tóm tắt</h2>
            <p>Tổng tiền tạm tính cho giỏ hàng hiện tại.</p>
          </div>
        </div>
        <div className="summary-total">
          <span>Tổng cộng</span>
          <strong>{formatVnd(total)}</strong>
        </div>
        <button
          className="primary-button checkout-button"
          type="button"
          disabled={isEmpty || isCreating}
          onClick={onCreateOrder}
        >
          <CheckCircle2 size={18} />
          {isCreating ? 'Đang đặt hàng...' : 'Đặt hàng'}
        </button>
        {orderError && <p className="form-error order-error">{orderError}</p>}
        <p className="summary-note">Sau khi đặt thành công, đơn hàng sẽ xuất hiện trong trang Đơn hàng.</p>
      </aside>
    </section>
  )
}

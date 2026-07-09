import { Eye, Package, RefreshCcw, ShoppingCart, Trophy } from 'lucide-react'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { formatVnd } from '../../utils/format.js'

export function ProductsPage({
  products,
  onRefreshProducts,
  onAddToCart,
  isLoading,
  error,
}) {
  const navigate = useNavigate()
  const [addingProductId, setAddingProductId] = useState(null)
  const topProducts = [...products].sort((a, b) => b.score - a.score).slice(0, 5)

  async function addProductToCart(productId) {
    setAddingProductId(productId)
    try {
      await onAddToCart(productId)
      navigate('/shop/cart')
    } finally {
      setAddingProductId(null)
    }
  }

  return (
    <section className="page-stack">
      <section className="panel">
        <div className="section-heading">
          <Package size={22} />
          <div>
            <h2>Sản phẩm</h2>
            <p>Chọn sản phẩm bạn muốn mua, hoặc mở trang chi tiết để tạo cache hot product.</p>
          </div>
          <button className="secondary-button" type="button" onClick={onRefreshProducts}>
            <RefreshCcw size={16} />
            Làm mới
          </button>
        </div>

        <div className="product-grid">
          {isLoading && <p className="state-text">Đang tải sản phẩm...</p>}
          {error && <p className="form-error">{error}</p>}
          {products.map((product) => {
            const isAdding = addingProductId === product.id

            return (
              <article className="product-card" key={product.id}>
                <div>
                  <span>{product.category}</span>
                  <h3>{product.name}</h3>
                  <p>{formatVnd(product.price)}</p>
                </div>
                <div className="product-meta">
                  <span>Còn {product.stock} sản phẩm</span>
                </div>
                <div className="product-actions">
                  <button
                    type="button"
                    className="secondary-button product-detail-button"
                    onClick={() => navigate(`/shop/products/${product.id}`)}
                  >
                    <Eye size={17} />
                    Xem chi tiết
                  </button>
                  <button
                    type="button"
                    className="add-cart-button"
                    disabled={isAdding}
                    onClick={() => addProductToCart(product.id)}
                  >
                    <ShoppingCart size={17} />
                    {isAdding ? 'Đang thêm...' : 'Thêm vào giỏ'}
                  </button>
                </div>
              </article>
            )
          })}
        </div>
      </section>

      <section className="panel ranking-panel">
        <div className="section-heading">
          <Trophy size={22} />
          <div>
            <h2>Sản phẩm bán chạy</h2>
            <p>Những sản phẩm được khách hàng đặt nhiều nhất.</p>
          </div>
        </div>
        <div className="ranking-list">
          {topProducts.map((product, index) => (
            <div key={product.id}>
              <strong>#{index + 1}</strong>
              <span>{product.name}</span>
              <b>{product.score}</b>
            </div>
          ))}
        </div>
      </section>
    </section>
  )
}

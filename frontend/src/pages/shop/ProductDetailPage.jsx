import { ArrowLeft, Package, ShoppingCart } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { getProduct } from '../../api/client.js'
import { formatVnd } from '../../utils/format.js'

export function ProductDetailPage({ onAddToCart }) {
  const { productId } = useParams()
  const [product, setProduct] = useState(null)
  const [error, setError] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [isAdding, setIsAdding] = useState(false)

  useEffect(() => {
    let isActive = true

    async function loadProduct() {
      setIsLoading(true)
      setError('')

      try {
        const nextProduct = await getProduct(productId)
        if (isActive) {
          setProduct(nextProduct)
        }
      } catch (exception) {
        if (isActive) {
          setError(exception.message)
        }
      } finally {
        if (isActive) {
          setIsLoading(false)
        }
      }
    }

    loadProduct()

    return () => {
      isActive = false
    }
  }, [productId])

  async function addToCart() {
    if (!product) {
      return
    }

    setIsAdding(true)
    try {
      await onAddToCart(product.id)
    } finally {
      setIsAdding(false)
    }
  }

  return (
    <section className="page-stack">
      <section className="panel product-detail-page">
        <div className="section-heading">
          <Package size={22} />
          <div>
            <h2>{product?.name ?? 'Chi tiết sản phẩm'}</h2>
            <p>{product?.description ?? 'Đang đọc cache riêng của sản phẩm từ Redis hoặc MySQL.'}</p>
          </div>
          <Link className="secondary-button link-button" to="/shop/products">
            <ArrowLeft size={16} />
            Quay lại
          </Link>
        </div>

        {isLoading && <p className="state-text">Đang tải chi tiết sản phẩm...</p>}
        {error && <p className="form-error">{error}</p>}

        {product && (
          <>
            <div className="product-detail-hero">
              <div>
                <span>{product.category}</span>
                <h3>{product.name}</h3>
                <p>{product.description}</p>
              </div>
              <strong>{formatVnd(product.price)}</strong>
            </div>

            <div className="product-detail-grid">
              <div>
                <span>Nguồn dữ liệu</span>
                <strong>{product.source}</strong>
              </div>
              <div>
                <span>TTL cache</span>
                <strong>{product.ttl}s</strong>
              </div>
              <div>
                <span>Tồn kho</span>
                <strong>{product.stock}</strong>
              </div>
              <div>
                <span>Điểm bán</span>
                <strong>{product.score}</strong>
              </div>
            </div>

            <button className="primary-button detail-add-button" type="button" onClick={addToCart} disabled={isAdding}>
              <ShoppingCart size={18} />
              {isAdding ? 'Đang thêm...' : 'Thêm vào giỏ'}
            </button>
          </>
        )}
      </section>
    </section>
  )
}

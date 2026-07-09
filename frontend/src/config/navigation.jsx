import { Boxes, Package, ShoppingCart } from 'lucide-react'

export const navGroups = [
  {
    label: 'Shop app',
    items: [
      { to: '/shop/products', label: 'Sản phẩm', icon: Package },
      { to: '/shop/cart', label: 'Giỏ hàng', icon: ShoppingCart },
      { to: '/shop/orders', label: 'Đơn hàng', icon: Boxes },
    ],
  },
]

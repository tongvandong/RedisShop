import { useState } from 'react'
import { LogIn } from 'lucide-react'
import { useNavigate } from 'react-router-dom'

export function LoginPage({ currentUser, onLogin }) {
  const navigate = useNavigate()
  const [form, setForm] = useState({ username: currentUser ?? 'user01', password: '123456' })
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function submit(event) {
    event.preventDefault()
    setError('')
    setIsSubmitting(true)

    try {
      await onLogin(form.username, form.password)
      navigate('/shop/products')
    } catch (exception) {
      setError(exception.message)
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main className="login-screen">
      <form className="login-card" onSubmit={submit}>
        <div className="login-title">
          <span className="login-mark">
            <LogIn size={22} />
          </span>
          <div>
            <p>Mini Shop</p>
            <h1>Đăng nhập</h1>
          </div>
        </div>

        <label>
          Username
          <input
            value={form.username}
            onChange={(event) => setForm({ ...form, username: event.target.value })}
            placeholder="user01"
          />
        </label>
        <label>
          Password
          <input
            type="password"
            value={form.password}
            onChange={(event) => setForm({ ...form, password: event.target.value })}
            placeholder="123456"
          />
        </label>
        {error && <p className="form-error">{error}</p>}
        <button className="primary-button" type="submit">
          <LogIn size={18} />
          {isSubmitting ? 'Đang xử lý...' : 'Đăng nhập'}
        </button>
      </form>
    </main>
  )
}

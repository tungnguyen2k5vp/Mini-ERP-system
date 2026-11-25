import { zodResolver } from '@hookform/resolvers/zod'
import { ArrowRight, Eye, EyeOff } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import type { z } from 'zod'
import { Brand } from '../../components/brand'
import { ErrorNotice } from '../../components/feedback'
import { Button } from '../../components/ui/button'
import { login } from '../../lib/api'
import { loginSchema } from './schemas'

export function Login() {
  const [error, setError] = useState<unknown>(null)
  const [showPassword, setShowPassword] = useState(false)
  const form = useForm<z.infer<typeof loginSchema>>({ resolver: zodResolver(loginSchema) })
  const { errors, isSubmitting } = form.formState

  return <div className="login-layout">
    <aside className="login-side" aria-label="MiniErp">
      <Brand />
      <div className="login-identity">
        <div className="login-mark" aria-hidden><div /><div /><div /></div>
        <h2>Quản lý<br />vận hành</h2>
        <p>Không gian làm việc dành cho doanh nghiệp phân phối.</p>
      </div>
    </aside>
    <main className="login-main">
      <div className="login-content">
        <div className="login-mobile-brand"><Brand /></div>
        <h1>Đăng nhập</h1>
        <p className="page-description">Dùng email công việc để vào MiniErp.</p>
        <form noValidate className="form-stack" onSubmit={form.handleSubmit(async values => {
          setError(null)
          try { await login(values.email, values.password) } catch (failure) { setError(failure) }
        })}>
          <div className="field">
            <label htmlFor="email">Email</label>
            <input id="email" type="email" autoComplete="username" autoCapitalize="none"
              aria-invalid={Boolean(errors.email)} aria-describedby={errors.email ? 'email-error' : undefined}
              {...form.register('email')} />
            {errors.email && <span className="field-error" id="email-error">{errors.email.message}</span>}
          </div>
          <div className="field">
            <label htmlFor="password">Mật khẩu</label>
            <div className="password-input">
              <input id="password" type={showPassword ? 'text' : 'password'} autoComplete="current-password"
                aria-invalid={Boolean(errors.password)} aria-describedby={errors.password ? 'password-error' : undefined}
                {...form.register('password')} />
              <button type="button" onClick={() => setShowPassword(!showPassword)}
                aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}>
                {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
              </button>
            </div>
            {errors.password && <span className="field-error" id="password-error">{errors.password.message}</span>}
          </div>
          <ErrorNotice error={error} />
          <Button type="submit" disabled={isSubmitting} className="login-submit">
            {isSubmitting ? 'Đang đăng nhập…' : 'Đăng nhập'}<ArrowRight size={17} aria-hidden />
          </Button>
        </form>
        <p className="login-help">Cần đặt lại mật khẩu? Liên hệ quản trị viên.</p>
      </div>
    </main>
  </div>
}

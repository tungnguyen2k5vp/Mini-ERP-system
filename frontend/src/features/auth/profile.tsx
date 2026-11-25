import { zodResolver } from '@hookform/resolvers/zod'
import { KeyRound, ShieldCheck, UserRound } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import type { z } from 'zod'
import { ErrorNotice } from '../../components/feedback'
import { Button } from '../../components/ui/button'
import { changePassword, type Profile } from '../../lib/api'
import { permissionLabel, roleLabel } from '../../lib/labels'
import { passwordSchema } from './schemas'

export function ProfilePage({ profile }: { profile: Profile }) {
  const [error, setError] = useState<unknown>(null)
  const form = useForm<z.infer<typeof passwordSchema>>({ resolver: zodResolver(passwordSchema) })
  const { errors, isSubmitting } = form.formState
  const fields = [
    { name: 'currentPassword', label: 'Mật khẩu hiện tại', autoComplete: 'current-password' },
    { name: 'newPassword', label: 'Mật khẩu mới', autoComplete: 'new-password' },
    { name: 'confirmPassword', label: 'Xác nhận mật khẩu mới', autoComplete: 'new-password' },
  ] as const

  return <>
    <header className="page-heading">
      <div><h1>Tài khoản của tôi</h1><p className="page-description">Xem thông tin đăng nhập và quyền làm việc của bạn.</p></div>
    </header>
    <div className="profile-grid">
      <section className="panel">
        <h2 className="section-heading"><UserRound size={19} aria-hidden /> Thông tin tài khoản</h2>
        <dl className="key-values">
          <div><dt>Họ và tên</dt><dd>{profile.displayName}</dd></div>
          <div><dt>Email</dt><dd>{profile.email}</dd></div>
          <div><dt>Vai trò</dt><dd>{profile.roles.map(roleLabel).join(', ') || 'Chưa có vai trò'}</dd></div>
        </dl>
      </section>
      <section className="panel profile-password">
        <h2 className="section-heading"><KeyRound size={19} aria-hidden /> Đổi mật khẩu</h2>
        <p className="muted">Dùng ít nhất 12 ký tự, có chữ hoa, chữ thường, chữ số và ký tự đặc biệt.</p>
        <form noValidate className="form-stack" onSubmit={form.handleSubmit(async ({ currentPassword, newPassword }) => {
          setError(null)
          try { await changePassword({ currentPassword, newPassword }) } catch (failure) { setError(failure) }
        })}>
          {fields.map(field => <div className="field" key={field.name}>
            <label htmlFor={field.name}>{field.label}</label>
            <input id={field.name} type="password" autoComplete={field.autoComplete}
              aria-invalid={Boolean(errors[field.name])} aria-describedby={errors[field.name] ? field.name + '-error' : undefined}
              {...form.register(field.name)} />
            {errors[field.name] && <span className="field-error" id={field.name + '-error'}>{errors[field.name]?.message}</span>}
          </div>)}
          <ErrorNotice error={error} />
          <div><Button type="submit" disabled={isSubmitting}>{isSubmitting ? 'Đang lưu…' : 'Cập nhật mật khẩu'}</Button></div>
        </form>
      </section>
      <section className="panel">
        <h2 className="section-heading"><ShieldCheck size={19} aria-hidden /> Quyền truy cập</h2>
        {profile.permissions.length > 0 ?
          <ul className="permission-list">{profile.permissions.map(permission => <li key={permission}>{permissionLabel(permission)}</li>)}</ul> :
          <p className="muted">Tài khoản chưa được cấp quyền.</p>}
      </section>
    </div>
  </>
}

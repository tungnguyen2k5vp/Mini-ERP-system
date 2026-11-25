import { z } from 'zod'

export const email = z.email('Nhập địa chỉ email hợp lệ.').max(254)
export const password = z.string().min(12, 'Mật khẩu phải có ít nhất 12 ký tự.').max(128)

export const loginSchema = z.object({
  email,
  password: z.string().min(1, 'Nhập mật khẩu.').max(128),
})

export const passwordSchema = z.object({
  currentPassword: z.string().min(1, 'Nhập mật khẩu hiện tại.').max(128),
  newPassword: password,
  confirmPassword: z.string(),
}).refine(data => data.newPassword === data.confirmPassword, {
  path: ['confirmPassword'],
  message: 'Mật khẩu xác nhận chưa khớp.',
})

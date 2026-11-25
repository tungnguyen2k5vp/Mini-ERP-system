import '@testing-library/jest-dom/vitest'
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it } from 'vitest'
import { Login } from './login'
import { passwordSchema } from './schemas'

afterEach(cleanup)

it('describes invalid fields before sending credentials', async () => {
  render(<Login />)
  await userEvent.click(screen.getByRole('button', { name: /^Đăng nhập$/ }))
  expect(await screen.findByText('Nhập địa chỉ email hợp lệ.')).toBeVisible()
  expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true')
})

it('reports a mismatched password confirmation on the matching field', () => {
  const result = passwordSchema.safeParse({
    currentPassword: 'old-password', newPassword: 'abcdef-123456', confirmPassword: 'different',
  })
  expect(result.success).toBe(false)
  if (!result.success) expect(result.error.issues[0]?.path).toEqual(['confirmPassword'])
})

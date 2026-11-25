import { expect, type Page } from '@playwright/test'

export async function login(page: Page) {
  const email = process.env.MINIERP_E2E_EMAIL
  const password = process.env.MINIERP_E2E_PASSWORD
  if (!email || !password) throw new Error('Set the development credentials using scripts/local-env.ps1.')
  await page.goto('/dang-nhap')
  await page.getByLabel('Email', { exact: true }).fill(email)
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password)
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Tài khoản của tôi' })).toBeVisible()
}

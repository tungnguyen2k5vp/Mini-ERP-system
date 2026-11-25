import { randomBytes } from 'node:crypto'
import { join } from 'node:path'
import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'
import { login } from './session'

test('real authentication, refresh after reload, and logout', async ({ page }) => {
  const errors: string[] = []
  page.on('pageerror', error => errors.push(error.message))

  await page.goto('/dang-nhap')
  await expect(page.getByRole('heading', { name: 'Đăng nhập' })).toBeVisible()
  await page.screenshot({ path: join('..', 'docs', 'screenshots', 'login-desktop.png') })
  await page.getByLabel('Email', { exact: true }).fill('unknown@minierp.example')
  await page.getByLabel('Mật khẩu', { exact: true }).fill('wrong-password')
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click()
  await expect(page.getByRole('alert')).toContainText('Kiểm tra thông tin đăng nhập')

  await login(page)
  const refresh = page.waitForResponse(response => response.url().endsWith('/auth/refresh'))
  await page.reload()
  expect((await refresh).status()).toBe(200)
  await expect(page.getByRole('heading', { name: 'Tài khoản của tôi' })).toBeVisible()
  await page.screenshot({ path: join('..', 'docs', 'screenshots', 'profile-desktop.png') })
  await page.getByRole('button', { name: 'Đăng xuất' }).click()
  await expect(page.getByRole('heading', { name: 'Đăng nhập' })).toBeVisible()
  expect(errors).toEqual([])
})

test('administrator creates an account from the users screen', async ({ page }) => {
  await login(page)
  await page.getByRole('link', { name: 'Người dùng', exact: true }).click()
  await page.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click()
  const form = page.locator('main form')
  const newEmail = 'browser-' + randomBytes(5).toString('hex') + '@minierp.example'
  await form.getByLabel('Họ và tên').fill('Nhân viên kiểm thử giao diện')
  await form.getByLabel('Email').fill(newEmail)
  await form.getByLabel('Mật khẩu ban đầu').fill(randomBytes(24).toString('base64') + 'Aa7!')
  const created = page.waitForResponse(response =>
    response.url().endsWith('/api/v1/users') && response.request().method() === 'POST')
  await form.getByRole('button', { name: 'Tạo tài khoản' }).click()
  expect((await created).status()).toBe(201)
  await expect(page.getByRole('status')).toHaveText('Đã tạo tài khoản.')
  await page.getByRole('searchbox', { name: 'Tìm tài khoản' }).fill(newEmail)
  await expect(page.getByRole('table').getByText(newEmail)).toBeVisible()
  await expect(page.getByRole('row')).toHaveCount(2)
  await page.screenshot({ path: join('..', 'docs', 'screenshots', 'users-desktop.png') })
})

test('mobile sign-in, keyboard navigation, and no page overflow', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await page.goto('/dang-nhap')
  await expect(page.getByRole('heading', { name: 'Đăng nhập' })).toBeVisible()
  await page.keyboard.press('Tab')
  await expect(page.getByLabel('Email', { exact: true })).toBeFocused()
  const audit = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze()
  expect(audit.violations.map(finding => finding.id)).toEqual([])
  await page.screenshot({ path: join('..', 'docs', 'screenshots', 'login-mobile.png') })
  await login(page)
  const toggle = page.getByRole('button', { name: 'Mở điều hướng' })
  await toggle.click()
  await expect(page.getByRole('dialog')).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(page.getByRole('dialog')).toBeHidden()
  await expect(toggle).toBeFocused()
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
  await page.screenshot({ path: join('..', 'docs', 'screenshots', 'profile-mobile.png') })
})

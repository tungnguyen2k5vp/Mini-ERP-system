import { randomBytes } from 'node:crypto'
import { join } from 'node:path'
import { expect, test } from '@playwright/test'
import { login } from './session'

test('warehouse sees logistics with redacted purchase prices on screen and print', async ({ page }) => {
  await login(page)
  await page.goto('/nguoi-dung')
  await page.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click()
  const form = page.locator('main form')
  const email = `warehouse-${randomBytes(5).toString('hex')}@minierp.example`
  const password = randomBytes(24).toString('base64') + 'Aa7!'
  await form.getByLabel('Họ và tên').fill('Kiểm tra quyền kho')
  await form.getByLabel('Email').fill(email)
  await form.getByLabel('Mật khẩu ban đầu').fill(password)
  await form.getByLabel('Vai trò').selectOption('Warehouse Staff')
  const created = page.waitForResponse(r => r.url().endsWith('/api/v1/users') && r.request().method() === 'POST')
  await form.getByRole('button', { name: 'Tạo tài khoản' }).click()
  expect((await created).status()).toBe(201)
  await page.getByRole('button', { name: 'Đăng xuất' }).click()
  await page.getByLabel('Email', { exact: true }).fill(email)
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password)
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Tài khoản của tôi' })).toBeVisible()
  const listing = page.waitForResponse(r => r.url().includes('/api/v1/orders?') && r.request().method() === 'GET')
  await page.goto('/mua-hang')
  const rows = (await (await listing).json()).items
  expect(rows.length).toBeGreaterThan(0)
  expect(rows.every((r: { total: number | null }) => r.total === null)).toBe(true)
  await expect(page.getByRole('columnheader', { name: 'Giá trị (VND)' }).getByRole('button')).toHaveCount(0)
  const detail = page.waitForResponse(r => r.url().endsWith(`/api/v1/orders/${rows[0].id}`))
  await page.getByRole('link', { name: rows[0].number, exact: true }).click()
  const body = await (await detail).json()
  expect(body.order.total).toBeNull()
  expect(body.lines.every((l: { unitPrice: number | null }) => l.unitPrice === null)).toBe(true)
  expect(body.fulfillments.every((f: { amount: number | null }) => f.amount === null)).toBe(true)
  const total = page.locator('.summary-strip > div').filter({ hasText: 'Giá trị đơn' }).locator('strong')
  await expect(total).toHaveText('— VND')
  for (const width of [1280, 390]) {
    await page.setViewportSize({ width, height: 844 })
    await expect(total).toHaveText('— VND')
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
    await page.screenshot({ path: join('..', '.work', `warehouse-purchase-${width}.png`), fullPage: true })
  }
  await page.emulateMedia({ media: 'print' })
  await expect(total).toHaveText('— VND')
  await expect(page.locator('main .number').filter({ hasText: /^—(?: VND)?$/ }).first()).toBeVisible()
  await page.screenshot({ path: join('..', '.work', 'warehouse-purchase-print.png'), fullPage: true })
})

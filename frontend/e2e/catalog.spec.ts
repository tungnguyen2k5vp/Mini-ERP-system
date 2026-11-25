import { randomBytes } from 'node:crypto'
import { join } from 'node:path'
import { expect, test } from '@playwright/test'
import { login } from './session'

test('create, find, and deactivate a product using the live catalog', async ({ page }) => {
  const unhandled: string[] = []
  page.on('pageerror', error => unhandled.push(error.message))
  page.on('response', response => {
    if (response.status() >= 500 && response.url().includes('/api/')) unhandled.push(response.status() + ' ' + response.url())
  })
  await login(page)
  await page.getByRole('link', { name: 'Sản phẩm', exact: true }).click()
  await expect(page.getByRole('table', { name: 'Sản phẩm' })).toBeVisible()
  await page.screenshot({ path: join('..', 'docs', 'screenshots', 'products-desktop.png') })

  await page.getByRole('link', { name: 'Thêm sản phẩm' }).click()
  const sku = 'BROWSER-' + randomBytes(4).toString('hex').toUpperCase()
  await page.getByLabel('SKU', { exact: true }).fill(sku)
  await page.getByLabel('Tên sản phẩm').fill('Bộ hồ sơ kiểm thử ' + sku)
  await page.getByLabel('Nhóm hàng').selectOption({ index: 1 })
  await page.getByLabel('Đơn vị tính').selectOption({ index: 1 })
  await page.getByLabel('Giá mua (VND)').fill('50000')
  await page.getByLabel('Giá bán (VND)').fill('85000')
  await page.getByLabel('Tồn tối thiểu').fill('10')
  await page.getByRole('button', { name: 'Lưu sản phẩm' }).click()
  await expect(page.getByRole('heading', { name: 'Bộ hồ sơ kiểm thử ' + sku })).toBeVisible()

  await page.getByRole('link', { name: 'Chỉnh sửa' }).click()
  await page.getByRole('checkbox', { name: 'Đang sử dụng' }).uncheck()
  await page.getByRole('button', { name: 'Lưu sản phẩm' }).click()
  await expect(page.getByText('Ngừng sử dụng', { exact: true })).toBeVisible()

  await page.getByRole('navigation', { name: 'Điều hướng chính' })
    .getByRole('link', { name: 'Sản phẩm', exact: true }).click()
  await page.getByLabel('Lọc trạng thái').selectOption('false')
  await page.getByRole('searchbox', { name: 'Tìm sản phẩm' }).fill(sku)
  await expect(page.getByRole('table').getByText(sku, { exact: true })).toBeVisible()
  await expect(page.getByRole('row')).toHaveCount(2)
  expect(unhandled).toEqual([])
})

test('catalog remains usable on a 390px screen', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await login(page)
  await page.goto('/san-pham')
  await expect(page.getByRole('table', { name: 'Sản phẩm' })).toBeVisible()
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
  await page.screenshot({ path: join('..', 'docs', 'screenshots', 'products-mobile.png') })
  await page.getByRole('button', { name: 'Mở điều hướng' }).click()
  await page.getByRole('dialog').getByRole('link', { name: 'Khách hàng', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Khách hàng', exact: true })).toBeVisible()
  const customer = page.getByRole('table').getByRole('link', { name: /Cửa hàng Mộc Lan/ }).first()
  await customer.click()
  await expect(page.getByText('Hạn mức công nợ')).toBeVisible()
})

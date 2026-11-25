import { join } from 'node:path'
import { expect, test } from '@playwright/test'
import { login } from './session'

test('post a transfer, see both ledger rows, and reverse it', async ({ page }) => {
  await login(page)
  await page.getByRole('link', { name: 'Tồn kho', exact: true }).click()
  await expect(page.getByRole('table', { name: 'Tồn kho theo sản phẩm' })).toBeVisible()
  await page.screenshot({ path: join('..', 'docs', 'screenshots', 'inventory-desktop.png') })

  await page.getByRole('navigation', { name: 'Điều hướng chính' })
    .getByRole('link', { name: 'Chứng từ kho', exact: true }).click()
  await page.getByRole('link', { name: 'Lập phiếu' }).click()
  await expect(page.getByRole('heading', { name: 'Lập phiếu kho', exact: true })).toBeVisible()
  await page.getByLabel('Loại phiếu', { exact: true }).selectOption('3')
  await expect(page.getByLabel('Loại phiếu', { exact: true })).toHaveValue('3')
  await page.getByLabel('Kho nguồn').selectOption({ label: 'Kho Hà Nội' })
  await page.getByLabel('Kho đích').selectOption({ label: 'Kho Hồ Chí Minh' })
  const product = page.getByLabel('Sản phẩm 1', { exact: true })
  const value = await product.getByRole('option', { name: /^VP-01-02/ }).getAttribute('value')
  expect(value).toBeTruthy()
  await product.selectOption(value!)
  await page.getByLabel('Số lượng', { exact: true }).fill('3')
  await page.getByRole('button', { name: 'Lưu phiếu nháp' }).click()
  const heading = page.getByRole('heading', { name: /^CK-/ })
  await expect(heading).toBeVisible()
  const number = await heading.innerText()

  page.once('dialog', dialog => void dialog.accept())
  await page.getByRole('button', { name: 'Ghi sổ', exact: true }).click()
  await expect(page.getByText('Đã ghi sổ', { exact: true })).toBeVisible()
  await page.screenshot({ path: join('..', 'docs', 'screenshots', 'stock-transfer-desktop.png') })

  await page.getByRole('link', { name: 'Biến động kho', exact: true }).click()
  const entries = page.getByRole('row').filter({ has: page.getByRole('link', { name: number, exact: true }) })
  await expect(entries).toHaveCount(2)
  await expect(entries.filter({ hasText: 'Kho Hà Nội' }).getByText('-3', { exact: true })).toBeVisible()
  await expect(entries.filter({ hasText: 'Kho Hồ Chí Minh' }).getByText('+3', { exact: true })).toBeVisible()
  await entries.first().getByRole('link', { name: number }).click()

  page.once('dialog', dialog => void dialog.accept())
  await page.getByRole('button', { name: 'Đảo chứng từ', exact: true }).click()
  await expect(page.getByRole('heading', { name: /^DA-/ })).toBeVisible()
  await expect(page.getByRole('link', { name: 'Mở chứng từ gốc' })).toBeVisible()
})

test('mobile inventory filters low stock with no page overflow', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await login(page)
  await page.goto('/ton-kho')
  await expect(page.getByRole('table', { name: 'Tồn kho theo sản phẩm' })).toBeVisible()
  const filtered = page.waitForResponse(response => response.url().includes('lowOnly=true'))
  await page.getByRole('checkbox', { name: 'Dưới định mức' }).check()
  expect((await filtered).status()).toBe(200)
  await expect(page.getByRole('row').getByText('Dưới định mức', { exact: true }).first()).toBeAttached()
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
  await page.screenshot({ path: join('..', 'docs', 'screenshots', 'inventory-mobile.png') })
})

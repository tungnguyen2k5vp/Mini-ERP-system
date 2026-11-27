import { expect, type Page } from '@playwright/test'

// Replenish only this test's shortage through the same receipt workflow as warehouse staff.
export async function ensureAvailableStock(page: Page, sku: string, warehouse: string, required: number) {
  await page.goto('/ton-kho')
  const filter = page.getByRole('combobox', { name: 'Lọc kho', exact: true })
  await filter.selectOption({ label: warehouse })
  const warehouseId = await filter.inputValue()
  const balances = page.waitForResponse(response => {
    const url = new URL(response.url())
    return url.pathname.endsWith('/inventory/balances') &&
      url.searchParams.get('warehouseId') === warehouseId && url.searchParams.get('search') === sku
  })
  await page.getByRole('searchbox', { name: 'Tìm tồn kho', exact: true }).fill(sku)
  const response = await balances
  expect(response.status()).toBe(200)
  const data = await response.json()
  const available = data.items.find((row: { sku: string }) => row.sku === sku)?.available ?? 0
  const missing = Math.max(0, required - available)
  if (!missing) return
  await page.goto('/ton-kho/chung-tu/moi')
  await page.getByLabel('Kho', { exact: true }).selectOption({ label: warehouse })
  const product = page.getByLabel('Sản phẩm 1', { exact: true })
  const option = product.getByRole('option').filter({ hasText: sku + ' ·' })
  const productId = await option.getAttribute('value')
  expect(productId).toBeTruthy()
  await product.selectOption(productId!)
  await page.getByLabel('Số lượng', { exact: true }).fill(String(missing))
  await page.getByLabel('Giá vốn (VND)', { exact: true }).fill('30000')
  await page.getByLabel('Ghi chú', { exact: true }).fill('Bổ sung phần thiếu cho kiểm tra trình duyệt')
  await page.getByRole('button', { name: 'Lưu phiếu nháp', exact: true }).click()
  await expect(page.getByRole('button', { name: 'Ghi sổ', exact: true })).toBeVisible()
  const posted = page.waitForResponse(result => result.request().method() === 'POST' && /\/inventory\/documents\/\d+\/post$/.test(result.url()))
  page.once('dialog', dialog => void dialog.accept())
  await page.getByRole('button', { name: 'Ghi sổ', exact: true }).click()
  expect((await posted).status()).toBe(200)
  await expect(page.getByText('Đã ghi sổ', { exact: true })).toBeVisible()
}

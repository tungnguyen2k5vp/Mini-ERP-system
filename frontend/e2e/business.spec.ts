import { join } from 'node:path'
import { expect, test, type Page } from '@playwright/test'
import AxeBuilder from '@axe-core/playwright'
import { login } from './session'

async function createOrder(page: Page, kind: 'mua' | 'ban', quantity: number) {
  await page.goto(`/${kind}-hang`)
  await page.getByRole('link', { name: 'Lập đơn mới' }).click()
  const partner = page.getByLabel(kind === 'mua' ? 'Nhà cung cấp' : 'Khách hàng', { exact: true })
  await partner.selectOption({ index: 1 })
  const partnerId = await partner.inputValue()
  await page.getByLabel('Kho thực hiện').selectOption({ label: 'Kho Hà Nội' })
  const product = page.getByLabel('Sản phẩm 1', { exact: true })
  const productId = await product.getByRole('option', { name: /^VP-01-02/ }).getAttribute('value')
  expect(productId).toBeTruthy()
  await product.selectOption(productId!)
  await page.getByLabel('Số lượng', { exact: true }).fill(String(quantity))
  await page.getByLabel('Đơn giá (VND)', { exact: true }).fill('30000')
  await page.getByRole('button', { name: 'Lưu đơn nháp' }).click()
  await expect(page.getByRole('heading', { name: /^(PO|SO)-/ })).toBeVisible()
  await page.getByRole('button', { name: 'Gửi duyệt' }).click()
  page.once('dialog', (dialog) => void dialog.accept())
  await page.getByRole('button', { name: 'Phê duyệt đơn' }).click()
  await expect(page.locator('.page-heading .badge')).toHaveText('Đã duyệt')
  return { url: page.url(), partnerId }
}

test('purchase partial receipt, payable allocation, return and refund through normal routes', async ({
  page,
}) => {
  test.setTimeout(90_000)
  await login(page)
  const { url: orderUrl, partnerId } = await createOrder(page, 'mua', 4)
  const quantity = page.locator('input[id^="fulfill-"]')
  await quantity.fill('2')
  page.once('dialog', (dialog) => void dialog.accept())
  await page.getByRole('button', { name: 'Ghi sổ nhận hàng' }).click()
  await expect(page.locator('.page-heading .badge')).toHaveText('Thực hiện một phần')
  await quantity.fill('2')
  page.once('dialog', (dialog) => void dialog.accept())
  await page.getByRole('button', { name: 'Ghi sổ nhận hàng' }).click()
  await expect(page.locator('.page-heading .badge')).toHaveText('Hoàn thành')
  await page.evaluate(() => window.scrollTo(0, 0))
  await page.screenshot({
    path: join('..', 'docs', 'screenshots', 'purchase-detail-desktop.png'),
    fullPage: true,
  })
  await page.emulateMedia({ media: 'print' })
  await expect(page.locator('.sidebar')).toBeHidden()
  await expect(page.getByRole('button', { name: 'In chứng từ' })).toBeHidden()
  await page.pdf({
    path: join('..', 'docs', 'screenshots', 'purchase-print.pdf'),
    format: 'A4',
    printBackground: true,
    tagged: true,
  })
  await page.emulateMedia({ media: 'screen' })
  await page.goto('/tai-chinh')
  await page.getByLabel('Loại công nợ').selectOption('2')
  await page.getByRole('combobox', { name: 'Lọc đối tác', exact: true }).selectOption(partnerId)
  await page.getByRole('button', { name: 'Lập phiếu thanh toán' }).click()
  await page.getByLabel('Phương thức', { exact: true }).selectOption({ index: 1 })
  const allocations = page.getByRole('spinbutton', { name: /^Phân bổ hóa đơn/ })
  await expect(allocations.first()).toBeVisible()
  // Allocate two source invoices in the same payment while preserving other existing records.
  await allocations.nth(0).fill('0.1')
  await allocations.nth(1).fill('0.2')
  const paymentRequest = page.waitForRequest(
    (request) => request.url().endsWith('/finance/payments') && request.method() === 'POST',
  )
  page.once('dialog', (dialog) => void dialog.accept())
  await page.getByRole('button', { name: 'Ghi sổ thanh toán' }).click()
  expect((await paymentRequest).postDataJSON().amount).toBe(0.3)
  await expect(
    page.getByRole('status').filter({ hasText: 'Đã ghi sổ và phân bổ thanh toán.' }),
  ).toBeVisible()
  await page.goto(orderUrl)
  await expect(page.getByRole('checkbox', { name: 'Lập phiếu trả hàng' })).toBeChecked()
  await page.locator('input[id^="fulfill-"]').fill('4')
  page.once('dialog', (dialog) => void dialog.accept())
  await page.getByRole('button', { name: 'Ghi sổ trả hàng' }).click()
  await expect(
    page.getByRole('list').filter({ has: page.getByText('Trả hàng', { exact: true }) }),
  ).toBeVisible()
  await page.goto('/tai-chinh')
  await page.getByLabel('Loại công nợ').selectOption('2')
  await page.getByRole('combobox', { name: 'Lọc đối tác', exact: true }).selectOption(partnerId)
  await page.getByRole('button', { name: 'Lập phiếu thanh toán' }).click()
  await page.getByRole('checkbox', { name: 'Hoàn tiền cho hóa đơn trả hàng' }).check()
  await page.getByLabel('Phương thức', { exact: true }).selectOption({ index: 1 })
  await page
    .getByRole('spinbutton', { name: /^Phân bổ hóa đơn/ })
    .first()
    .fill('0.1')
  page.once('dialog', (dialog) => void dialog.accept())
  await page.getByRole('button', { name: 'Ghi sổ thanh toán' }).click()
  await expect(page.getByText('Đã ghi sổ và phân bổ thanh toán.', { exact: true })).toBeVisible()
})

test('lost order creation response retries the same request without creating a duplicate', async ({
  page,
}) => {
  await login(page)
  await page.goto('/mua-hang/moi')
  await page.getByLabel('Nhà cung cấp', { exact: true }).selectOption({ index: 1 })
  await page.getByLabel('Kho thực hiện').selectOption({ index: 1 })
  await page.getByLabel('Sản phẩm 1', { exact: true }).selectOption({ index: 1 })
  const ids: string[] = []
  let createdId = 0
  await page.route('**/api/v1/orders', async (route) => {
    if (route.request().method() !== 'POST') return route.continue()
    ids.push(route.request().postDataJSON().requestId)
    if (ids.length === 1) {
      const response = await route.fetch()
      expect(response.status()).toBe(201)
      createdId = (await response.json()).order.id
      return route.abort('failed')
    }
    return route.continue()
  })
  await page.getByRole('button', { name: 'Lưu đơn nháp' }).click()
  await expect(page.getByRole('alert')).toContainText('Mất kết nối')
  await page.getByRole('button', { name: 'Lưu đơn nháp' }).click()
  await expect(page).toHaveURL(new RegExp('/mua-hang/' + createdId + '$'))
  expect(ids).toHaveLength(2)
  expect(ids[0]).toBe(ids[1])
})

test('sales reserve and partial shipment, mobile business workspaces and accessible dashboard', async ({
  page,
}) => {
  test.setTimeout(90_000)
  await login(page)
  await createOrder(page, 'ban', 2)
  await expect(
    page
      .getByRole('table', { name: 'Dòng đơn hàng' })
      .getByRole('row')
      .nth(1)
      .getByRole('cell')
      .nth(4),
  ).toHaveText('2')
  await page.locator('input[id^="fulfill-"]').fill('1')
  page.once('dialog', (dialog) => void dialog.accept())
  await page.getByRole('button', { name: 'Ghi sổ giao hàng' }).click()
  await expect(page.locator('.page-heading .badge')).toHaveText('Thực hiện một phần')
  await page.evaluate(() => window.scrollTo(0, 0))
  await page.screenshot({
    path: join('..', 'docs', 'screenshots', 'sales-detail-desktop.png'),
    fullPage: true,
  })
  await page.goto('/tong-quan')
  await expect(page.getByRole('heading', { name: 'Tổng quan hoạt động' })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Cần xử lý' })).toBeVisible()
  const firstDay = page
    .getByRole('group', { name: 'Biểu đồ số lượng nhập xuất theo ngày' })
    .getByRole('button')
    .first()
  await firstDay.focus()
  await expect(firstDay).toHaveAttribute('aria-pressed', 'true')
  await expect(page.locator('.chart-readout')).toBeVisible()
  await page.evaluate(() => window.scrollTo(0, 0))
  await page.screenshot({
    path: join('..', 'docs', 'screenshots', 'dashboard-desktop.png'),
    fullPage: true,
  })
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([])
  for (const route of [
    '/mua-hang',
    '/ban-hang',
    '/tai-chinh',
    '/bao-cao',
    '/nhat-ky',
    '/tim-kiem?q=VP',
    '/san-pham',
  ]) {
    await page.goto(route)
    await expect(page.locator('main h1')).toBeVisible()
    await expect(page.locator('.loading')).toHaveCount(0)
    await expect(page.locator('.notice-error')).toHaveCount(0)
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
    await page.evaluate(() => window.scrollTo(0, 0))
    await page.screenshot({
      path: join('..', 'docs', 'screenshots', route.split('?')[0]!.slice(1) + '-desktop.png'),
      fullPage: true,
    })
  }
  await page.setViewportSize({ width: 390, height: 844 })
  for (const route of [
    '/tong-quan',
    '/mua-hang',
    '/ban-hang',
    '/tai-chinh',
    '/bao-cao',
    '/nhat-ky',
    '/tim-kiem?q=VP',
    '/san-pham',
  ]) {
    await page.goto(route)
    await expect(page.locator('main h1')).toBeVisible()
    await expect(page.locator('.loading')).toHaveCount(0)
    await expect(page.locator('.notice-error')).toHaveCount(0)
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
    await page.evaluate(() => window.scrollTo(0, 0))
    await page.screenshot({
      path: join('..', 'docs', 'screenshots', route.split('?')[0]!.slice(1) + '-mobile.png'),
      fullPage: true,
    })
  }
  for (const width of [320, 768]) {
    await page.setViewportSize({ width, height: 900 })
    await page.goto('/tong-quan')
    await expect(page.locator('.loading')).toHaveCount(0)
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
    await page.evaluate(() => window.scrollTo(0, 0))
    await page.screenshot({
      path: join('..', 'docs', 'screenshots', `dashboard-${width}.png`),
      fullPage: true,
    })
  }
})

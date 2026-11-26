import { randomBytes } from 'node:crypto'
import { expect, test } from '@playwright/test'
import axe from 'axe-core'
import { businessDate } from '../src/lib/format'
import { login } from './session'

declare global {
  interface Window { axe: typeof axe }
}

test.use({ timezoneId: 'Pacific/Honolulu' })

test('P0 admin sees current stock diagnostics, dated financial exceptions and six responsive widths', async ({
  page,
}) => {
  test.setTimeout(180_000)
  await login(page)
  const asOfDate = businessDate()
  const stockResponse = page.waitForResponse((response) =>
    response.url().includes('/reports/stock/reconciliation?'),
  )
  await page.goto('/bao-cao?from=2025-01-01&to=2025-01-31')
  await expect(page.getByLabel('Từ ngày', { exact: true })).toHaveValue('2025-01-01')
  await page.getByRole('link', { name: 'Đối soát hiện tại', exact: true }).click()
  const stock = await stockResponse
  expect(stock.status()).toBe(200)
  const stockData = await stock.json()
  await expect(page.getByRole('heading', { name: 'Đối soát tồn kho hiện tại' })).toBeVisible()
  await expect(page.getByLabel('Từ ngày', { exact: true })).toHaveCount(0)
  await expect(page.getByLabel('Đến ngày', { exact: true })).toHaveCount(0)
  if (stockData.total === 0) {
    await expect(
      page.getByRole('status').filter({ hasText: 'Không phát hiện sai lệch tồn kho' }),
    ).toBeVisible()
  } else {
    await expect(page.getByRole('table', { name: 'Sai lệch tồn kho hiện tại' })).toBeVisible()
    await expect(
      page.getByRole('link', { name: stockData.items[0].sku, exact: true }),
    ).toHaveAttribute('href', new RegExp('warehouseId=' + stockData.items[0].warehouseId))
  }
  const warehouse = page.getByRole('combobox', { name: 'Kho báo cáo', exact: true })
  const warehouseId = await warehouse.getByRole('option').nth(1).getAttribute('value')
  expect(warehouseId).toBeTruthy()
  const filtered = page.waitForResponse(
    (response) =>
      response.url().includes('/reports/stock/reconciliation?') &&
      response.url().includes('warehouseId=' + warehouseId),
  )
  await warehouse.selectOption(warehouseId!)
  expect((await filtered).status()).toBe(200)

  const financeResponse = page.waitForResponse(
    (response) =>
      response.url().includes('/finance/reconciliation/exceptions?') &&
      response.url().includes('asOfDate=' + asOfDate),
  )
  await page.goto(`/tai-chinh?tab=reconciliation&direction=1&asOfDate=${asOfDate}`)
  const finance = await financeResponse
  expect(finance.status()).toBe(200)
  const financeData = await finance.json()
  expect(financeData.cutoff.businessDate).toBe(asOfDate)
  expect(financeData.cutoff.inclusive).toBe(false)
  await expect(page.getByRole('tab', { name: 'Đối soát', exact: true })).toHaveAttribute(
    'aria-selected',
    'true',
  )
  await expect(page.getByLabel('Tính đến', { exact: true })).toHaveValue(asOfDate)
  if (financeData.total === 0)
    await expect(
      page.getByRole('status').filter({ hasText: 'Không phát hiện ngoại lệ tài chính' }),
    ).toBeVisible()
  else await expect(page.getByRole('table', { name: 'Ngoại lệ đối soát tài chính' })).toBeVisible()
  const routes = [
    ['stock', '/bao-cao?view=reconciliation'],
    ['finance', `/tai-chinh?tab=reconciliation&direction=1&asOfDate=${asOfDate}`],
    ['aging', `/tai-chinh?tab=aging&direction=2&asOfDate=${asOfDate}`],
    ['dashboard', '/tong-quan'],
  ] as const
  for (const [name, route] of routes) {
    await page.goto(route)
    await expect(page.locator('main h1')).toBeVisible()
    await expect(page.locator('.loading')).toHaveCount(0)
    await expect(page.getByRole('alert')).toHaveCount(0)
    for (const width of [320, 390, 768, 1024, 1280, 1440]) {
      await page.setViewportSize({ width, height: 900 })
      const measured = await page.evaluate(async () => {
        await new Promise(requestAnimationFrame)
        await new Promise(requestAnimationFrame)
        return innerWidth
      })
      if (measured !== width) {
        // Windows WebKit scales the host window. Measure its actual CSS width before asserting layout.
        await page.setViewportSize({ width: Math.round(width * width / measured), height: Math.round(900 * width / measured) })
      }
      await expect.poll(() => page.evaluate(() => innerWidth)).toBe(width)
      expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth),
        `${name} at ${width} CSS pixels`).toBe(0)
      if (name === 'dashboard') {
        await expect(
          page.getByRole('heading', { name: 'Phiếu nhập · xuất theo ngày' }),
        ).toBeVisible()
        await expect(page.getByLabel('Đến ngày', { exact: true })).toHaveValue(asOfDate)
      }
      await page.evaluate(() => window.scrollTo(0, 0))
      await page.screenshot({
        path: test.info().outputPath(`v2-p0-${name}-${width}.png`),
        fullPage: true,
      })
    }
  }
})

test('financial reconciliation passes the WCAG accessibility checks', async ({ browser }, info) => {
  const context = await browser.newContext({
    baseURL: info.project.use.baseURL, locale: 'vi-VN', timezoneId: 'Asia/Ho_Chi_Minh',
  })
  try {
    const page = await context.newPage()
    await login(page)
    await page.goto('/tai-chinh?tab=reconciliation&direction=1&asOfDate=' + businessDate())
    await expect(page.getByRole('heading', { name: 'Ngoại lệ đối soát theo chứng từ' })).toBeVisible()
    await expect(page.locator('.loading')).toHaveCount(0)
    await expect(page.getByRole('alert')).toHaveCount(0)
    await page.addScriptTag({ content: axe.source })
    const violations = await page.evaluate(async () => (await window.axe.run(document, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21aa'] },
      resultTypes: ['violations'],
    })).violations.map(v => v.id))
    expect(violations).toEqual([])
  } finally {
    await context.close()
  }
})

test('P0 stock diagnostics report transport errors and recover through the read-only retry action', async ({
  page,
}) => {
  test.setTimeout(45_000)
  await login(page)
  let fail = true
  await page.route('**/api/v1/reports/stock/reconciliation?**', (route) =>
    fail ? route.abort('failed') : route.continue(),
  )
  await page.goto('/bao-cao?view=reconciliation')
  await expect(page.getByRole('alert').filter({ hasText: 'Mất kết nối' })).toBeVisible({
    timeout: 15_000,
  })
  fail = false
  const response = page.waitForResponse((result) =>
    result.url().includes('/reports/stock/reconciliation?'),
  )
  await page.getByRole('button', { name: 'Kiểm tra lại tồn kho' }).click()
  expect((await response).status()).toBe(200)
  await expect(page.getByRole('alert')).toHaveCount(0)
})

test('P0 Viewer can inspect stock diagnostics but cannot enter financial reconciliation', async ({
  page,
  browser,
}) => {
  test.setTimeout(60_000)
  await login(page)
  const email = 'p0-viewer-' + randomBytes(6).toString('hex') + '@minierp.example'
  const password = randomBytes(24).toString('base64') + 'Aa7!'
  await page.goto('/nguoi-dung')
  await page.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click()
  const form = page.locator('main form')
  await form.getByLabel('Họ và tên').fill('Kiểm tra đối soát chỉ đọc')
  await form.getByLabel('Email', { exact: true }).fill(email)
  await form.getByLabel('Mật khẩu ban đầu').fill(password)
  await form.getByLabel('Vai trò', { exact: true }).selectOption('Viewer')
  await form.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click()
  await expect(page.getByRole('status')).toHaveText('Đã tạo tài khoản.')
  const context = await browser.newContext({
    baseURL: test.info().project.use.baseURL,
    timezoneId: 'Pacific/Honolulu',
  })
  try {
    const viewer = await context.newPage()
    await viewer.goto('/dang-nhap')
    await viewer.getByLabel('Email', { exact: true }).fill(email)
    await viewer.getByLabel('Mật khẩu', { exact: true }).fill(password)
    await viewer.getByRole('button', { name: 'Đăng nhập', exact: true }).click()
    await expect(viewer.getByRole('heading', { name: 'Tổng quan hoạt động' })).toBeVisible()
    await expect(
      viewer.getByRole('heading', { name: 'Doanh thu, giá vốn & lợi nhuận' }),
    ).toHaveCount(0)
    const response = viewer.waitForResponse((result) =>
      result.url().includes('/reports/stock/reconciliation?'),
    )
    await viewer.goto('/bao-cao?view=reconciliation')
    expect((await response).status()).toBe(200)
    await expect(viewer.getByRole('alert')).toHaveCount(0)
    await viewer.goto('/tai-chinh?tab=reconciliation')
    await expect(viewer.getByRole('heading', { name: 'Bạn chưa có quyền truy cập' })).toBeVisible()
  } finally {
    await context.close()
  }
})

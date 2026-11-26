import { expect, test } from 'vitest'
import { businessDate, calendarDate, date, money } from './format'

test('Vietnam business calendar crosses the day and year at 17:00 UTC', () => {
  expect(businessDate('2025-12-31T16:59:59.999Z')).toBe('2025-12-31')
  expect(businessDate('2025-12-31T17:00:00.000Z')).toBe('2026-01-01')
  expect(calendarDate('2026-01-01')).toBe('1/1/2026')
  expect(date('2025-12-31T17:00:00.000Z')).toContain('00:00:00')
  expect(date('2025-12-31T17:00:00.000Z')).toContain('1/1/2026')
  expect(money(null)).toBe('—')
  expect(money(0)).toBe('0')
})

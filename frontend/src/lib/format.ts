export const money = (n: number | null) =>
  n === null ? '—' : new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 4 }).format(n)
const businessTimeZone = 'Asia/Ho_Chi_Minh'
export const businessDate = (value: string | Date = new Date()) =>
  new Intl.DateTimeFormat('en-CA', { timeZone: businessTimeZone }).format(new Date(value))
export const calendarDate = (value: string) =>
  new Intl.DateTimeFormat('vi-VN', { timeZone: businessTimeZone }).format(
    new Date(/^\d{4}-\d{2}-\d{2}$/.test(value) ? value + 'T00:00:00+07:00' : value),
  )
export const date = (value: string) =>
  new Date(value).toLocaleString('vi-VN', { timeZone: businessTimeZone })

export const money = (n: number) =>
  new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 4 }).format(n)
export const date = (value: string) => new Date(value).toLocaleString('vi-VN')

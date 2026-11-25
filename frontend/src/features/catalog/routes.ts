export const records = {
  categories: { path: '/nhom-hang', title: 'Nhóm hàng', singular: 'nhóm hàng' },
  units: { path: '/don-vi', title: 'Đơn vị tính', singular: 'đơn vị tính' },
  warehouses: { path: '/kho', title: 'Kho hàng', singular: 'kho hàng' },
  'payment-methods': { path: '/phuong-thuc-thanh-toan', title: 'Phương thức thanh toán', singular: 'phương thức thanh toán' },
  customers: { path: '/khach-hang', title: 'Khách hàng', singular: 'khách hàng' },
  suppliers: { path: '/nha-cung-cap', title: 'Nhà cung cấp', singular: 'nhà cung cấp' },
} as const

export type Resource = keyof typeof records

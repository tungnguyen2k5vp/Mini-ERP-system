const roleLabels: Record<string, string> = {
  Administrator: 'Quản trị viên',
  Manager: 'Quản lý',
  'Sales Staff': 'Nhân viên bán hàng',
  'Purchasing Staff': 'Nhân viên mua hàng',
  'Warehouse Staff': 'Nhân viên kho',
  Accountant: 'Kế toán',
  Viewer: 'Người xem',
}

export const roleLabel = (role: string) => roleLabels[role] ?? role

const modules: Record<string, string> = {
  Catalog: 'danh mục', Inventory: 'kho hàng', Purchasing: 'mua hàng',
  Sales: 'bán hàng', Finance: 'công nợ', Reports: 'báo cáo',
}

const actions: Record<string, string> = {
  View: 'Xem', Create: 'Tạo', Update: 'Sửa', Export: 'Xuất', Post: 'Ghi sổ', Approve: 'Duyệt', Cancel: 'Hủy',
}

export function permissionLabel(permission: string) {
  const special: Record<string, string> = {
    'Sales.OverrideCreditLimit': 'Duyệt bán hàng vượt hạn mức công nợ',
    'Finance.ChangeCreditLimit': 'Thay đổi hạn mức công nợ khách hàng',
    'Finance.Reverse': 'Đảo phiếu thu chi đã ghi sổ',
    'System.Users': 'Quản lý người dùng',
    'System.Roles': 'Quản lý phân quyền',
    'System.Audit': 'Xem nhật ký hoạt động',
    'Reports.Sensitive': 'Xem báo cáo tài chính',
    'Orders.SelfApprove': 'Duyệt đơn do chính mình lập, sửa hoặc gửi duyệt',
    'Orders.ApproveAboveLimit': 'Duyệt đơn vượt hạn mức phê duyệt',
  }
  if (special[permission]) return special[permission]
  const [module = '', action = ''] = permission.split('.')
  return modules[module] && actions[action] ? actions[action] + ' ' + modules[module] : permission
}

const currencyFormat = new Intl.NumberFormat('vi-VN', {
  style: 'currency', currency: 'VND', minimumFractionDigits: 0, maximumFractionDigits: 4,
}).format
export const formatMoney = (value: number | null) => value === null ? '—' : currencyFormat(value)

export const formatQuantity = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 4 }).format

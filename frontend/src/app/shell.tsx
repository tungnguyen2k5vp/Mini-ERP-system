import { Boxes, Building2, ChevronRight, ClipboardList, CreditCard, FileText, History, LogOut, Menu, Package, Ruler,
  ShieldCheck, Tags, UserRound, UsersRound, Warehouse, X } from 'lucide-react'
import { Suspense, useRef, useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router'
import { Brand } from '../components/brand'
import { ErrorNotice, Loading } from '../components/feedback'
import { Button } from '../components/ui/button'
import { logout, type Profile } from '../lib/api'
import { roleLabel } from '../lib/labels'

const navigation = [
  { group: 'Kho vận', items: [
    { name: 'Tồn kho', path: '/ton-kho', icon: Boxes, permission: 'Inventory.View' },
    { name: 'Chứng từ kho', path: '/ton-kho/chung-tu', icon: FileText, permission: 'Inventory.View' },
    { name: 'Biến động kho', path: '/ton-kho/lich-su', icon: History, permission: 'Inventory.View' },
  ] },
  { group: 'Danh mục', items: [
    { name: 'Sản phẩm', path: '/san-pham', icon: Package, permission: 'Catalog.View' },
    { name: 'Nhóm hàng', path: '/nhom-hang', icon: Tags, permission: 'Catalog.View' },
    { name: 'Đơn vị tính', path: '/don-vi', icon: Ruler, permission: 'Catalog.View' },
    { name: 'Kho hàng', path: '/kho', icon: Warehouse, permission: 'Catalog.View' },
    { name: 'Phương thức thanh toán', path: '/phuong-thuc-thanh-toan', icon: CreditCard, permission: 'Catalog.View' },
  ] },
  { group: 'Đối tác', items: [
    { name: 'Khách hàng', path: '/khach-hang', icon: Building2, permission: 'Catalog.View' },
    { name: 'Nhà cung cấp', path: '/nha-cung-cap', icon: ClipboardList, permission: 'Catalog.View' },
  ] },
  { group: 'Tài khoản', items: [
    { name: 'Hồ sơ cá nhân', path: '/tai-khoan', icon: UserRound, permission: '' },
  ] },
  { group: 'Quản trị hệ thống', items: [
    { name: 'Người dùng', path: '/nguoi-dung', icon: UsersRound, permission: 'System.Users' },
    { name: 'Vai trò và quyền', path: '/vai-tro', icon: ShieldCheck, permission: 'System.Roles' },
  ] },
]

export function AppShell({ profile }: { profile: Profile }) {
  const dialog = useRef<HTMLDialogElement>(null)
  const location = useLocation()
  const active = navigation.flatMap(section => section.items).filter(item =>
    location.pathname === item.path || location.pathname.startsWith(item.path + '/'))
    .sort((left, right) => right.path.length - left.path.length)[0]
  const [loggingOut, setLoggingOut] = useState(false)
  const [exitError, setExitError] = useState<unknown>(null)
  const initials = profile.displayName.trim().split(/\s+/).slice(-2).map(word => word[0]).join('').toUpperCase()

  async function signOut() {
    setExitError(null)
    setLoggingOut(true)
    dialog.current?.close()
    try { await logout() } catch (error) { setExitError(error); setLoggingOut(false) }
  }

  return <div className="app-shell">
    <a className="skip-link" href="#main">Đến nội dung chính</a>
    <aside className="sidebar">
      <div className="sidebar-brand"><Brand /></div>
      <Navigation profile={profile} />
      <div className="sidebar-footer"><Button type="button" variant="ghost" disabled={loggingOut} onClick={() => void signOut()}>
        <LogOut size={18} aria-hidden /> Đăng xuất</Button></div>
    </aside>
    <dialog id="mobile-navigation" className="mobile-nav" aria-label="Điều hướng" ref={dialog}
      onClick={event => { if (event.target === event.currentTarget) event.currentTarget.close() }}>
      <div className="mobile-nav-body">
        <div className="sidebar-brand"><Brand /><Button variant="ghost" type="button" className="icon-button"
          aria-label="Đóng điều hướng" onClick={() => dialog.current?.close()}><X size={20} /></Button></div>
        <Navigation profile={profile} onNavigate={() => dialog.current?.close()} />
        <div className="sidebar-footer"><Button type="button" variant="ghost" disabled={loggingOut} onClick={() => void signOut()}>
          <LogOut size={18} aria-hidden /> Đăng xuất</Button></div>
      </div>
    </dialog>
    <div className="workspace">
      <header className="topbar">
        <div className="flex min-w-0 items-center gap-3">
          <Button variant="ghost" type="button" className="icon-button nav-toggle" aria-label="Mở điều hướng"
            aria-haspopup="dialog" aria-controls="mobile-navigation" onClick={() => dialog.current?.showModal()}><Menu size={22} /></Button>
          <nav aria-label="Đường dẫn" className="breadcrumb"><span>MiniErp</span><ChevronRight size={14} aria-hidden />
            <span aria-current="page">{active?.name ?? 'Trang hiện tại'}</span></nav>
        </div>
        <NavLink to="/tai-khoan" className="topbar-account" aria-label="Hồ sơ cá nhân">
          <span className="avatar" aria-hidden>{initials}</span>
          <span><strong>{profile.displayName}</strong><small>{profile.roles.map(roleLabel).join(', ')}</small></span>
        </NavLink>
      </header>
      <main className="workspace-main" id="main" tabIndex={-1}>
        <ErrorNotice error={exitError} />
        <Suspense fallback={<Loading />}><Outlet /></Suspense>
      </main>
    </div>
  </div>
}

function Navigation({ profile, onNavigate }: { profile: Profile; onNavigate?: () => void }) {
  return <nav className="side-nav" aria-label="Điều hướng chính">
    {navigation.map(section => {
      const items = section.items.filter(item => !item.permission || profile.permissions.includes(item.permission))
      return items.length > 0 && <div className="side-group" key={section.group}>
        <p>{section.group}</p>
        {items.map(item => <NavLink key={item.path} to={item.path} onClick={onNavigate} end={item.path === '/ton-kho'}>
          <item.icon size={18} aria-hidden /> {item.name}
        </NavLink>)}
      </div>
    })}
  </nav>
}

import { lazy, Suspense, useEffect, useState, useSyncExternalStore, type ReactNode } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router'
import { ErrorNotice, Loading } from '../components/feedback'
import { Button } from '../components/ui/button'
import { records, type Resource } from '../features/catalog/routes'
import { currentSession, onSessionChange, refreshSession, type Profile } from '../lib/api'
import { AppShell } from './shell'

const Login = lazy(() => import('../features/auth/login').then(module => ({ default: module.Login })))
const ProfilePage = lazy(() => import('../features/auth/profile').then(module => ({ default: module.ProfilePage })))
const UsersPage = lazy(() => import('../features/admin/users').then(module => ({ default: module.UsersPage })))
const RolesPage = lazy(() => import('../features/admin/roles').then(module => ({ default: module.RolesPage })))
const ProductsPage = lazy(() => import('../features/catalog/products').then(module => ({ default: module.ProductsPage })))
const ProductDetail = lazy(() => import('../features/catalog/products').then(module => ({ default: module.ProductDetail })))
const ProductForm = lazy(() => import('../features/catalog/products').then(module => ({ default: module.ProductForm })))
const RecordsPage = lazy(() => import('../features/catalog/records').then(module => ({ default: module.RecordsPage })))
const RecordDetail = lazy(() => import('../features/catalog/records').then(module => ({ default: module.RecordDetail })))
const RecordForm = lazy(() => import('../features/catalog/records').then(module => ({ default: module.RecordForm })))
const InventoryPage = lazy(() => import('../features/inventory/inventory').then(module => ({ default: module.InventoryPage })))
const HistoryPage = lazy(() => import('../features/inventory/inventory').then(module => ({ default: module.HistoryPage })))
const DocumentsPage = lazy(() => import('../features/inventory/inventory').then(module => ({ default: module.DocumentsPage })))
const StockDocumentPage = lazy(() => import('../features/inventory/inventory').then(module => ({ default: module.StockDocumentPage })))
const StockForm = lazy(() => import('../features/inventory/stock-form').then(module => ({ default: module.StockForm })))
const OrdersPage = lazy(() => import('../features/orders/orders').then(module => ({ default: module.OrdersPage })))
const OrderForm = lazy(() => import('../features/orders/orders').then(module => ({ default: module.OrderForm })))
const OrderDetail = lazy(() => import('../features/orders/orders').then(module => ({ default: module.OrderDetail })))
const FinancePage = lazy(() => import('../features/finance/finance').then(module => ({ default: module.FinancePage })))
const DashboardPage = lazy(() => import('../features/reports/reports').then(module => ({ default: module.DashboardPage })))
const ReportsPage = lazy(() => import('../features/reports/reports').then(module => ({ default: module.ReportsPage })))
const AuditPage = lazy(() => import('../features/reports/reports').then(module => ({ default: module.AuditPage })))
const SearchPage = lazy(() => import('../features/reports/reports').then(module => ({ default: module.SearchPage })))

export function App() {
  const session = useSyncExternalStore(onSessionChange, currentSession)
  const [error, setError] = useState<unknown>(null)
  useEffect(() => {
    if (currentSession() === undefined) void refreshSession().catch(setError)
  }, [])

  if (session === undefined) return <main className="session-screen">
    {error ? <div>
      <ErrorNotice error={error} />
      <Button onClick={() => { setError(null); void refreshSession().catch(setError) }}>Thử lại</Button>
    </div> : <Loading label="Đang kiểm tra phiên" />}
  </main>

  return <BrowserRouter>
    {session ? <Routes>
      <Route element={<AppShell profile={session.user} />}>
        <Route index element={<Navigate to={session.user.permissions.includes('Reports.View') ? '/tong-quan' : '/tai-khoan'} replace />} />
        <Route path="dang-nhap" element={<Navigate to={session.user.permissions.includes('Reports.View') ? '/tong-quan' : '/tai-khoan'} replace />} />
        <Route path="tong-quan" element={session.user.permissions.includes('Reports.View') ? <DashboardPage profile={session.user} /> : <Navigate to="/tai-khoan" replace />} />
        <Route path="bao-cao" element={can(session.user, 'Reports.View', can(session.user, 'Inventory.View', <ReportsPage profile={session.user} />))} />
        <Route path="nhat-ky" element={can(session.user, 'System.Audit', <AuditPage />)} />
        <Route path="tim-kiem" element={<SearchPage />} />
        <Route path="tai-chinh" element={can(session.user, 'Finance.View', <FinancePage profile={session.user} />)} />
        {[{ path: 'mua-hang', kind: 1, grant: 'Purchasing' }, { path: 'ban-hang', kind: 2, grant: 'Sales' }].flatMap(({ path, kind, grant }) => [
          <Route key={path} path={path} element={can(session.user, grant + '.View', <OrdersPage key={kind} kind={kind} profile={session.user} />)} />,
          <Route key={path + '/moi'} path={path + '/moi'} element={can(session.user, grant + '.Create', <OrderForm key={kind} kind={kind} />)} />,
          <Route key={path + '/:id'} path={path + '/:id'} element={can(session.user, grant + '.View', <OrderDetail key={kind} kind={kind} profile={session.user} />)} />,
        ])}
        <Route path="tai-khoan" element={<ProfilePage profile={session.user} />} />
        <Route path="nguoi-dung" element={can(session.user, 'System.Users', <UsersPage />)} />
        <Route path="vai-tro" element={can(session.user, 'System.Roles', <RolesPage />)} />
        <Route path="san-pham" element={can(session.user, 'Catalog.View', <ProductsPage profile={session.user} />)} />
        <Route path="san-pham/moi" element={can(session.user, 'Catalog.Create', <ProductForm />)} />
        <Route path="san-pham/:id" element={can(session.user, 'Catalog.View', <ProductDetail profile={session.user} />)} />
        <Route path="san-pham/:id/sua" element={can(session.user, 'Catalog.Update', <ProductForm />)} />
        <Route path="ton-kho" element={can(session.user, 'Inventory.View', <InventoryPage />)} />
        <Route path="ton-kho/lich-su" element={can(session.user, 'Inventory.View', <HistoryPage />)} />
        <Route path="ton-kho/chung-tu" element={can(session.user, 'Inventory.View', <DocumentsPage profile={session.user} />)} />
        <Route path="ton-kho/chung-tu/:id" element={can(session.user, 'Inventory.View', <StockDocumentPage profile={session.user} />)} />
        <Route path="ton-kho/chung-tu/moi" element={can(session.user, 'Inventory.View',
          can(session.user, 'Inventory.Post', <StockForm />))} />
        {(Object.keys(records) as Resource[]).flatMap(resource => {
          const path = records[resource].path
          return [
            <Route key={path} path={path} element={can(session.user, 'Catalog.View',
              <RecordsPage key={resource} resource={resource} profile={session.user} />)} />,
            <Route key={path + '/moi'} path={path + '/moi'} element={can(session.user, 'Catalog.Create', <RecordForm resource={resource} profile={session.user} />)} />,
            <Route key={path + '/:id'} path={path + '/:id'} element={can(session.user, 'Catalog.View',
              <RecordDetail resource={resource} profile={session.user} />)} />,
            <Route key={path + '/:id/sua'} path={path + '/:id/sua'} element={can(session.user, 'Catalog.Update', <RecordForm resource={resource} profile={session.user} />)} />,
          ]
        })}
        <Route path="403" element={<StatusPage code="403" title="Bạn chưa có quyền truy cập" detail="Liên hệ quản trị viên nếu cần quyền cho trang này." />} />
        <Route path="*" element={<StatusPage code="404" title="Không tìm thấy trang" detail="Kiểm tra đường dẫn hoặc chọn một mục trong thanh điều hướng." />} />
      </Route>
    </Routes> : <Routes>
      <Route path="dang-nhap" element={<Suspense fallback={<Loading label="Đang mở trang đăng nhập" />}><Login /></Suspense>} />
      <Route path="*" element={<Navigate to="/dang-nhap" replace />} />
    </Routes>}
  </BrowserRouter>
}

function can(profile: Profile, permission: string, child: ReactNode) {
  return profile.permissions.includes(permission) ? child : <Navigate to="/403" replace />
}

function StatusPage({ code, title, detail }: { code: string; title: string; detail: string }) {
  return <section className="panel status-page">
    <p className="muted">{code}</p>
    <h1>{title}</h1><p className="page-description">{detail}</p>
    <Button asChild variant="outline"><a href="/tai-khoan">Về hồ sơ cá nhân</a></Button>
  </section>
}

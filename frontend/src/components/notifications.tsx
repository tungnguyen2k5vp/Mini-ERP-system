import { useQuery } from '@tanstack/react-query'
import { Bell } from 'lucide-react'
import { Link } from 'react-router'
import { api } from '../lib/api'
import { ErrorNotice, Loading } from './feedback'

export function NotificationMenu() {
  const query = useQuery({
    queryKey: ['dashboard-notifications'],
    queryFn: ({ signal }) =>
      api<{ alerts: { label: string; count: number; href: string }[] }>('/reports/dashboard?', {
        signal,
      }),
    staleTime: 30_000,
  })
  const alerts = query.data?.alerts.filter((a) => a.count > 0) ?? []
  const close = () => document.getElementById('operation-notifications')?.hidePopover()
  return (
    <div className="notifications">
      <button
        type="button"
        className="notification-trigger"
        popoverTarget="operation-notifications"
        aria-label={'Thông báo, ' + alerts.length + ' mục cần xử lý'}
      >
        <Bell size={19} aria-hidden />
        {alerts.length > 0 && <span>{alerts.length}</span>}
      </button>
      <div id="operation-notifications" popover="auto" className="notification-popover">
        <h2>Thông báo vận hành</h2>
        <ErrorNotice error={query.error} />
        {query.isPending ? (
          <Loading />
        ) : alerts.length ? (
          <ul className="work-queue">
            {alerts.map((a) => (
              <li key={a.label}>
                <Link to={a.href} onClick={close}>
                  <span>{a.label}</span>
                  <strong>{a.count}</strong>
                </Link>
              </li>
            ))}
          </ul>
        ) : (
          <p className="muted">Không có việc cần xử lý.</p>
        )}
        <Link className="record-link" to="/tong-quan" onClick={close}>
          Mở tổng quan
        </Link>
      </div>
    </div>
  )
}

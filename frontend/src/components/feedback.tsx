import { AlertCircle, Inbox } from 'lucide-react'
import { ApiError } from '../lib/api'

export function ErrorNotice({ error }: { error: unknown }) {
  if (!error) return null
  const fields = error instanceof ApiError ? Object.values(error.problem.errors ?? {}).flat() : []
  return <div className="notice notice-error" role="alert">
    <AlertCircle aria-hidden size={18} />
    <div>
      {fields.length > 0 ? fields.join(' ') :
        error instanceof ApiError ? error.message : 'Đã có lỗi. Hãy thử lại.'}
      {error instanceof ApiError && error.status >= 500 && error.problem.correlationId &&
        <small className="block">Mã đối chiếu {error.problem.correlationId}</small>}
    </div>
  </div>
}

export function EmptyState({ children }: { children: string }) {
  return <div className="empty-state"><Inbox size={24} aria-hidden /><p>{children}</p></div>
}

export function Loading({ label = 'Đang tải dữ liệu' }: { label?: string }) {
  return <div className="loading" role="status"><span className="loading-mark" aria-hidden />{label}…</div>
}

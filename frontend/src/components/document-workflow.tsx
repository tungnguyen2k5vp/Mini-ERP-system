import { date } from '../lib/format'
import { Button } from './ui/button'

const actions: Record<string, string> = {
  CreateOrder: 'Lập nháp', EditOrder: 'Sửa nháp', submitOrder: 'Gửi duyệt',
  approveOrder: 'Phê duyệt', rejectOrder: 'Từ chối',
  'return-for-revisionOrder': 'Yêu cầu sửa', cancelOrder: 'Hủy đơn',
  FulfillOrder: 'Thực hiện đơn', ReturnOrder: 'Trả hàng', ReverseFulfillment: 'Đảo giao dịch',
  CreateStockDocument: 'Lập nháp', EditStockDocument: 'Sửa nháp',
  PostStockDocument: 'Ghi sổ', CancelStockDocument: 'Hủy nháp', ReverseStockDocument: 'Đảo chứng từ',
}

type Event = { action: string; atUtc: string; actorName: string | null; reason: string | null }

export function DocumentTimeline({ title, history }: { title: string; history?: Event[] }) {
  return <section className="panel section-gap">
    <h2>{title}</h2>
    {history?.length ? <ol className="document-timeline" aria-label={title}>
      {history.map((entry, index) => <li key={index}>
        <div>
          <strong>{actions[entry.action] ?? entry.action}</strong>
          <time dateTime={entry.atUtc}>{date(entry.atUtc)}</time>
        </div>
        <span>{entry.actorName || 'Chưa rõ người thực hiện'}</span>
        {entry.reason && <p>{entry.reason}</p>}
      </li>)}
    </ol> : <p className="muted">Chưa có lịch sử chứng từ.</p>}
  </section>
}

export function ConflictNotice({ onReload }: { onReload: () => void }) {
  return <div className="notice form-stack">
    <p>Nội dung chưa lưu được giữ nguyên. Bạn có thể mở chứng từ trong thẻ khác để đối chiếu. Tải bản mới sẽ bỏ toàn bộ thay đổi chưa lưu.</p>
    <div><Button type="button" variant="outline" onClick={() => {
      if (window.confirm('Bỏ toàn bộ thay đổi chưa lưu và tải nội dung mới nhất?')) onReload()
    }}>Bỏ thay đổi và tải bản mới</Button></div>
  </div>
}

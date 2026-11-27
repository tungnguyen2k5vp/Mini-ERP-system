import { CreditExposurePanel, useCreditExposure } from '../finance/credit-exposure'
import { ConflictNotice, DocumentTimeline } from '../../components/document-workflow'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Plus, Printer, Search, Trash2 } from 'lucide-react'
import type { SortingState } from '@tanstack/react-table'
import { useRef, useState, type FormEvent } from 'react'
import {
  Link,
  Navigate,
  useBeforeUnload,
  useNavigate,
  useParams,
  useSearchParams,
} from 'react-router'
import { DataTable } from '../../components/data-table'
import { ErrorNotice, Loading } from '../../components/feedback'
import { Button } from '../../components/ui/button'
import { api, json, type Page, type Profile } from '../../lib/api'
import { queryClient } from '../../lib/query'
import { useDebouncedValue } from '../../lib/use-debounced-value'

import { calendarDate, date, money } from '../../lib/format'
type Order = {
  id: number
  number: string
  kind: number
  status: number
  partnerId: string
  partnerName: string
  warehouseId: string
  expectedDate: string | null
  isOverdue: boolean
  warehouseName: string
  total: number | null
  createdAtUtc: string
  version: string
}
type Line = {
  productId: string
  sku: string
  productName: string
  quantity: number
  unitPrice: number | null
  fulfilledQuantity: number
  returnedQuantity: number
  reservedQuantity: number
}
type Detail = {
  canApprove?: boolean
  history?: { action: string; atUtc: string; actorId: string; actorName: string; reason: string | null }[]
  order: Order
  note: string | null
  lines: Line[]
  fulfillments: {
    id: number
    stockDocumentId: number
    isReturn: boolean
    reversesId: number | null
    createdAtUtc: string
    amount: number | null
  }[]
}
const statuses = ['', 'Nháp', 'Chờ duyệt', 'Đã duyệt', 'Thực hiện một phần', 'Hoàn thành', 'Đã hủy', 'Từ chối', 'Yêu cầu sửa']
const base = (kind: number) => (kind === 1 ? '/mua-hang' : '/ban-hang')
const prefix = (kind: number) => (kind === 1 ? 'Purchasing' : 'Sales')
function useOrderOptions(kind: number) {
  return useQuery({
    queryKey: ['orders', 'options', kind],
    queryFn: ({ signal }) =>
      api<{
        partners: { id: string; name: string; isActive: boolean }[]
        warehouses: { id: string; name: string }[]
        products: {
          id: string
          sku: string
          name: string
          purchasePrice: number | null
          salePrice: number
        }[]
      }>('/orders/options?kind=' + kind, { signal }),
  })
}
export function OrderBadge({ status }: { status: number }) {
  return (
    <span
      className={
        'badge ' + (status === 5 ? 'badge-good' : status === 7 ? 'badge-error' : status === 8 ? 'badge-revision' : status === 6 ? 'badge-neutral' : 'badge-warn')
      }
    >
      {statuses[status]}
    </span>
  )
}

export function OrdersPage({ kind, profile }: { kind: number; profile: Profile }) {
  const [params, setParams] = useSearchParams()
  const [page, setPage] = useState(1)
  const options = useOrderOptions(kind)
  const partners = { ...options, data: options.data?.partners }
  const partnerId = params.get('partnerId') ?? ''
  const [text, setText] = useState('')
  const search = useDebouncedValue(text)
  const onlyOverdue = params.get('overdue') === 'true' || params.get('onlyOverdue') === 'true'
  const status = params.get('status') ?? ''
  function filter(name: string, value: string) {
    setParams(previous => {
      const next = new URLSearchParams(previous)
      if (name === 'onlyOverdue') next.delete('overdue')
      if (value) next.set(name, value)
      else next.delete(name)
      return next
    })
    setPage(1)
  }
  const [sorting, setSorting] = useState<SortingState>([{ id: 'createdAtUtc', desc: true }])
  const parameters = new URLSearchParams({
    kind: String(kind),
    page: String(page),
    pageSize: '15',
    search,
    sort: sorting[0]?.id ?? 'createdAtUtc',
    descending: String(sorting[0]?.desc ?? true),
  })
  if (partnerId) parameters.set('partnerId', partnerId)
  if (status) parameters.set('status', status)
  if (onlyOverdue) parameters.set('onlyOverdue', 'true')
  const query = useQuery({
    queryKey: ['orders', kind, parameters.toString()],
    queryFn: ({ signal }) => api<Page<Order>>('/orders?' + parameters, { signal }),
  })
  return (
    <>
      <header className="page-heading">
        <div>
          <h1>{kind === 1 ? 'Đơn mua hàng' : 'Đơn bán hàng'}</h1>
          <p className="page-description">
            Theo dõi phê duyệt, tiến độ {kind === 1 ? 'nhận' : 'giao'} hàng và chứng từ nguồn.
          </p>
        </div>
        {profile.permissions.includes(prefix(kind) + '.Create') && (
          <Button asChild>
            <Link to={base(kind) + '/moi'}>
              <Plus size={17} /> Lập đơn mới
            </Link>
          </Button>
        )}
      </header>
      <div className="table-toolbar">
        <label className="search-box">
          <Search size={17} aria-hidden />
          <span className="sr-only">Tìm đơn hàng</span>
          <input
            type="search"
            maxLength={120}
            placeholder="Số đơn hoặc tên đối tác"
            value={text}
            onChange={(e) => {
              setText(e.target.value)
              setPage(1)
            }}
          />
        </label>
        <label className="filter-select">
          <span className="sr-only">Lọc đối tác</span>
          <select
            value={partnerId}
            onChange={(e) => {
              filter('partnerId', e.target.value)
              setPage(1)
            }}
          >
            <option value="">Tất cả {kind === 1 ? 'nhà cung cấp' : 'khách hàng'}</option>
            {partners.data?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </select>
        </label>
        <label className="filter-select">
          <span className="sr-only">Trạng thái đơn</span>
          <select
            value={status}
            onChange={(e) => {
              filter('status', e.target.value)
              setPage(1)
            }}
          >
            <option value="">Tất cả trạng thái</option>
            {statuses.slice(1).map((s, i) => (
              <option key={i} value={i + 1}>
                {s}
              </option>
            ))}
          </select>
        </label>
        <label className="checkbox-row"><input type="checkbox" checked={onlyOverdue} onChange={e => { filter('onlyOverdue', e.target.checked ? 'true' : '') }} /> Chỉ đơn quá hạn</label>
      </div>
      <ErrorNotice error={query.error ?? partners.error} />
      {query.isPending ? (
        <Loading />
      ) : (
        query.data && (
          <DataTable
            label="Danh sách đơn hàng"
            page={query.data}
            onPage={setPage}
            hiddenColumns
            sorting={sorting}
            onSorting={(value) => {
              setSorting(value)
              setPage(1)
            }}
            columns={[
              {
                accessorKey: 'number',
                header: 'Số đơn',
                cell: ({ row }) => (
                  <Link className="record-link" to={base(kind) + '/' + row.original.id}>
                    {row.original.number}
                  </Link>
                ),
              },
              { accessorKey: 'partnerName', header: 'Đối tác', enableSorting: false },
              { accessorKey: 'warehouseName', header: 'Kho', enableSorting: false },
              { accessorKey: 'expectedDate', header: 'Ngày dự kiến', enableSorting: false, cell: ({ row }) => <>{row.original.expectedDate ? calendarDate(row.original.expectedDate) : '—'} {row.original.isOverdue && <span className="badge badge-warn">Quá hạn</span>}</> },
              {
                accessorKey: 'status',
                header: 'Trạng thái',
                cell: ({ row }) => <OrderBadge status={row.original.status} />,
              },
              {
                accessorKey: 'total',
                header: 'Giá trị (VND)',
                enableSorting: kind === 2 || profile.permissions.some(p =>
                  ['Reports.Sensitive', 'Purchasing.Create', 'Purchasing.Approve'].includes(p)),
                cell: ({ row }) => <span className="number">{money(row.original.total)}</span>,
              },
              {
                accessorKey: 'createdAtUtc',
                header: 'Ngày lập',
                cell: ({ row }) => date(row.original.createdAtUtc),
              },
            ]}
          />
        )
      )}
    </>
  )
}

export function OrderForm({ kind, initial, onSaved, onReload }: { kind: number; initial?: Detail; onSaved?: () => void; onReload?: () => void }) {
  const navigate = useNavigate()
  const request = useRef({ id: crypto.randomUUID(), signature: '' })
  const [dirty, setDirty] = useState(false)
  useBeforeUnload((e) => {
    if (dirty) {
      e.preventDefault()
      e.returnValue = true
    }
  })
  const [lines, setLines] = useState(initial?.lines.map(l => ({ productId: l.productId, quantity: l.quantity, unitPrice: l.unitPrice ?? 0 })) ?? [{ productId: '', quantity: 1, unitPrice: 0 }])
  const options = useOrderOptions(kind)
  const partners = { ...options, data: options.data?.partners }
  const warehouses = { ...options, data: options.data?.warehouses }
  const products = { ...options, data: options.data?.products }
  const save = useMutation({
    mutationFn: (value: object) => api<Detail>(initial ? '/orders/' + initial.order.id : '/orders', json(initial ? 'PUT' : 'POST', value)),
    onSuccess: (result) => {
      setDirty(false)
      onSaved?.()
      void queryClient.invalidateQueries({ queryKey: ['orders'] })
      void navigate(base(kind) + '/' + result.order.id)
    },
  })
  function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const form = new FormData(e.currentTarget)
    const body = {
      partnerId: form.get('partnerId'),
      warehouseId: form.get('warehouseId'),
      note: form.get('note'),
      expectedDate: form.get('expectedDate') || null,
      lines,
    }
    const signature = JSON.stringify(body)
    if (request.current.signature !== signature)
      request.current = { id: crypto.randomUUID(), signature }
    save.mutate(initial ? { ...body, version: initial.order.version } : { ...body, kind, requestId: request.current.id })
  }
  return (
    <>
      <Link className="back-link" to={base(kind)}>
        Quay lại danh sách
      </Link>
      <header className="page-heading">
        <div>
          <h1>{initial ? 'Sửa đơn' : 'Lập đơn'} {kind === 1 ? 'mua' : 'bán'} hàng</h1>
          <p className="page-description">Đơn nháp chưa tác động đến tồn kho hoặc công nợ.</p>
        </div>
      </header>
      <ErrorNotice error={partners.error ?? warehouses.error ?? products.error} />
      {partners.isPending || warehouses.isPending || products.isPending ? (
        <Loading />
      ) : (
        <form className="panel" onChange={() => setDirty(true)} onSubmit={submit}>
          <fieldset className="form-stack" disabled={save.isPending}>
            <div className="form-grid">
              <div className="field">
                <label htmlFor="order-partner">{kind === 1 ? 'Nhà cung cấp' : 'Khách hàng'}</label>
                <select id="order-partner" name="partnerId" defaultValue={initial?.order.partnerId} required>
                  <option value="">Chọn đối tác</option>
                  {partners.data
                    ?.filter((p) => p.isActive)
                    .map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.name}
                      </option>
                    ))}
                </select>
              </div>
              <div className="field">
                <label htmlFor="order-warehouse">Kho thực hiện</label>
                <select id="order-warehouse" name="warehouseId" defaultValue={initial?.order.warehouseId} required>
                  <option value="">Chọn kho</option>
                  {warehouses.data?.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="field">
                <label htmlFor="order-note">Ghi chú</label>
                <input id="order-note" name="note" maxLength={300} defaultValue={initial?.note ?? ''} />
              </div>
              <div className="field"><label htmlFor="order-expected">Ngày dự kiến</label><input id="order-expected" name="expectedDate" type="date" defaultValue={initial?.order.expectedDate?.slice(0, 10) ?? ''} />
              </div>
            </div>
            <fieldset className="stock-lines">
              <legend>Hàng hóa</legend>
              {lines.map((line, i) => (
                <div className="stock-line" key={i}>
                  <div className="field stock-line-product">
                    <label htmlFor={'ol-product-' + i}>Sản phẩm {i + 1}</label>
                    <select
                      id={'ol-product-' + i}
                      required
                      value={line.productId}
                      onChange={(e) => {
                        const p = products.data?.find((p) => p.id === e.target.value)
                        setLines(
                          lines.map((l, j) =>
                            j === i
                              ? {
                                  ...l,
                                  productId: e.target.value,
                                  unitPrice:
                                    kind === 1 ? (p?.purchasePrice ?? 0) : (p?.salePrice ?? 0),
                                }
                              : l,
                          ),
                        )
                      }}
                    >
                      <option value="">Chọn sản phẩm</option>
                      {products.data
                        ?.filter(
                          (p) =>
                            p.id === line.productId || !lines.some((l) => l.productId === p.id),
                        )
                        .map((p) => (
                          <option key={p.id} value={p.id}>
                            {p.sku} · {p.name}
                          </option>
                        ))}
                    </select>
                  </div>
                  <div className="field">
                    <label htmlFor={'ol-qty-' + i}>Số lượng</label>
                    <input
                      id={'ol-qty-' + i}
                      type="number"
                      min="0.0001"
                      max="999999999"
                      step="0.0001"
                      required
                      value={Number.isFinite(line.quantity) ? line.quantity : ''}
                      onChange={(e) =>
                        setLines(
                          lines.map((l, j) =>
                            j === i ? { ...l, quantity: e.target.valueAsNumber } : l,
                          ),
                        )
                      }
                    />
                  </div>
                  <div className="field">
                    <label htmlFor={'ol-price-' + i}>Đơn giá (VND)</label>
                    <input
                      id={'ol-price-' + i}
                      type="number"
                      min="0"
                      max="999999999999"
                      step="0.0001"
                      required
                      value={Number.isFinite(line.unitPrice) ? line.unitPrice : ''}
                      onChange={(e) =>
                        setLines(
                          lines.map((l, j) =>
                            j === i ? { ...l, unitPrice: e.target.valueAsNumber } : l,
                          ),
                        )
                      }
                    />
                  </div>
                  <Button
                    type="button"
                    variant="ghost"
                    className="icon-button"
                    disabled={lines.length === 1}
                    aria-label={'Xóa dòng ' + (i + 1)}
                    onClick={() => setLines(lines.filter((_, j) => j !== i))}
                  >
                    <Trash2 size={17} />
                  </Button>
                </div>
              ))}
            </fieldset>
            <div>
              <Button
                type="button"
                variant="outline"
                disabled={lines.length >= 50}
                onClick={() => setLines([...lines, { productId: '', quantity: 1, unitPrice: 0 }])}
              >
                <Plus size={17} /> Thêm dòng
              </Button>
            </div>
            <div className="document-total">
              Giá trị tạm tính{' '}
              <strong>
                {money(lines.reduce((n, l) => n + (l.quantity * l.unitPrice || 0), 0))} VND
              </strong>
            </div>
            <ErrorNotice error={save.error} />
            {save.error && initial && <ConflictNotice onReload={() => onReload?.()} /> }
            <div className="form-actions">
              <Button type="button" variant="outline" onClick={() => {
                if (!dirty || window.confirm('Bỏ thay đổi chưa lưu?')) {
                  if (onSaved) onSaved()
                  else void navigate(base(kind))
                }
              }}>Hủy</Button>
              <Button disabled={save.isPending}>
                {save.isPending ? 'Đang lưu…' : 'Lưu đơn nháp'}
              </Button>
            </div>
          </fieldset>
        </form>
      )}
    </>
  )
}

export function OrderDetail({ kind, profile }: { kind: number; profile: Profile }) {
  const { id } = useParams()
  return <OrderDetailContent key={id} kind={kind} profile={profile} />
}

function OrderDetailContent({ kind, profile }: { kind: number; profile: Profile }) {
  const { id } = useParams()
  const [editing, setEditing] = useState<Detail | null>(null)
  const [reason, setReason] = useState('')
  const [reasonAction, setReasonAction] = useState('')
  const [reasonVersion, setReasonVersion] = useState('')
  const [isReturn, setReturn] = useState(false)
  const [quantities, setQuantities] = useState<Record<string, string>>({})
  const [requestId, setRequestId] = useState(() => crypto.randomUUID())
  const [success, setSuccess] = useState('')
  const query = useQuery({
    queryKey: ['orders', 'detail', id],
    queryFn: ({ signal }) => api<Detail>('/orders/' + id, { signal }),
  })
  const canViewCredit = kind === 2 && (profile.permissions.includes('Sales.Approve') || (profile.permissions.includes('Finance.View') && profile.permissions.includes('Sales.View')))
  const credit = useCreditExposure('/orders/' + id + '/credit-exposure', canViewCredit)
  const action = useMutation({
    mutationFn: ({ path, body }: { path: string; body: unknown }) =>
      api('/orders/' + id + path, json('POST', body)),
    onSuccess: () => {
      setRequestId(crypto.randomUUID())
      setQuantities({})
      setSuccess('Đã cập nhật chứng từ.')
      setReasonAction('')
      setReason('')
      void queryClient.invalidateQueries()
    },
  })
  if (query.isPending) return <Loading />
  if (!query.data) return <ErrorNotice error={query.error} />
  if (editing) return <OrderForm key={editing.order.id} kind={kind} initial={editing} onSaved={() => setEditing(null)} onReload={() => { void query.refetch().then(result => { if (result.isSuccess) setEditing(null) }) }} />
  const { order, lines, fulfillments } = query.data
  if (order.kind !== kind) return <Navigate to={base(order.kind) + '/' + id} replace />
  const returning = isReturn || order.status === 5 || order.status === 6
  const allowed = (grant: string) => profile.permissions.includes(prefix(kind) + '.' + grant)
  const post = (path: string, body: unknown, confirm?: string) => {
    if (!confirm || window.confirm(confirm)) {
      setSuccess('')
      action.mutate({ path, body })
    }
  }
  return (
    <>
      <Link className="back-link" to={base(kind)}>
        Quay lại danh sách
      </Link>
      <header className="page-heading">
        <div>
          <h1>{order.number}</h1>
          <p className="page-description">
            {profile.permissions.includes('Catalog.View') ? (
              <Link
                className="record-link"
                to={(kind === 1 ? '/nha-cung-cap/' : '/khach-hang/') + order.partnerId}
              >
                {order.partnerName}
              </Link>
            ) : (
              order.partnerName
            )}{' '}
            · {order.warehouseName}
          </p>
        </div>
        <div className="action-row">
          <OrderBadge status={order.status} />
          <Button variant="outline" onClick={() => window.print()}>
            <Printer size={16} /> In chứng từ
          </Button>
        </div>
      </header>
      <ErrorNotice error={query.error ?? action.error} />
      {action.error && (
        <Button
          variant="outline"
          onClick={() => {
            action.reset()
            void query.refetch()
          }}
        >
          Tải lại chứng từ
        </Button>
      )}
      {success && (
        <p className="notice notice-good" role="status">
          {success}
        </p>
      )}
      {[6, 7, 8].includes(order.status) ? <section className="panel section-gap" aria-label="Kết quả xét duyệt">
        <h2>{statuses[order.status]}</h2>
        {query.data.history?.findLast(entry => entry.reason)?.reason &&
          <p>{query.data.history.findLast(entry => entry.reason)?.reason}</p>}
        <p className="muted">{order.status === 8 ? 'Sửa đơn nháp để cập nhật nội dung và gửi duyệt lại.' : order.status === 7 ? 'Đơn đã bị từ chối và không tiếp tục thực hiện.' : 'Phần chưa thực hiện đã dừng; lịch sử đã ghi sổ được giữ lại.'}</p>
      </section> : <ol className="workflow" aria-label="Tiến trình đơn hàng">
        {[1, 2, 3, 4, 5].map((s) => (
          <li
            key={s}
            aria-current={order.status === s ? 'step' : undefined}
            className={order.status >= s && order.status <= 5 ? 'reached' : ''}
          >
            {statuses[s]}
          </li>
        ))}
      </ol>}
      <section className="panel document-summary">
        <div className="summary-strip">
          <div>
            <span>Đối tác</span>
            <strong>{order.partnerName}</strong>
          </div>
          <div>
            <span>Ngày lập</span>
            <strong>{date(order.createdAtUtc)}</strong>
          </div>
          <div>
            <span>Giá trị đơn</span>
            <strong>{money(order.total)} VND</strong>
          </div>
        </div>
        <div className="form-actions">
          {[1, 8].includes(order.status) && allowed('Create') && <Button variant="outline" onClick={() => setEditing(query.data)}>Sửa đơn nháp</Button>}
          {order.status === 1 && allowed('Create') && (
            <Button
              disabled={action.isPending}
              onClick={() => post('/submit', { version: order.version })}
            >
              Gửi duyệt
            </Button>
          )}
          {order.status === 2 && allowed('Approve') && query.data.canApprove !== false && (
            <Button
              disabled={action.isPending || (kind === 2 && (!credit.data || credit.isError || credit.isFetching || (credit.data.requiresOverride && !profile.permissions.includes('Sales.OverrideCreditLimit'))))}
              onClick={() => {
                if (kind === 2 && credit.data?.requiresOverride) { setReason(''); setReasonVersion(order.version); setReasonAction('approve'); return }
                post(
                  '/approve',
                  { version: order.version },
                  kind === 1
                    ? 'Phê duyệt đơn mua hàng này?'
                    : 'Phê duyệt đơn bán và giữ hàng trong kho?',
                )
              }}
            >
              Phê duyệt đơn
            </Button>
          )}
          {order.status === 2 && allowed('Approve') && <>
            <Button variant="outline" disabled={action.isPending} onClick={() => { setReason(''); setReasonVersion(order.version); setReasonAction('reject') }}>Từ chối</Button>
            <Button variant="outline" disabled={action.isPending} onClick={() => { setReason(''); setReasonVersion(order.version); setReasonAction('return-for-revision') }}>Yêu cầu sửa</Button>
          </>}
          {[1, 2, 3, 4, 8].includes(order.status) && allowed('Cancel') &&
            <Button variant="outline" disabled={action.isPending} onClick={() => { setReason(''); setReasonVersion(order.version); setReasonAction('cancel') }}>Hủy đơn</Button>}
        </div>
        {order.status === 2 && allowed('Approve') && query.data.canApprove === false &&
          <p className="muted">Đơn cần người duyệt độc lập hoặc người có quyền duyệt theo hạn mức.</p>}
        {reasonAction && <form className="form-stack section-gap" onSubmit={e => {
          e.preventDefault()
          const blockedCredit = reasonAction === 'approve' && (!credit.data || credit.isError || credit.isFetching || !profile.permissions.includes('Sales.OverrideCreditLimit'))
          if (reason.trim() && !blockedCredit) post('/' + reasonAction, { version: reasonVersion, reason: reason.trim() }, 'Xác nhận thay đổi trạng thái đơn hàng?')
        }}>
          <div className="field">
            <label htmlFor="order-action-reason">Lý do {reasonAction === 'approve' ? 'duyệt vượt hạn mức' : reasonAction === 'cancel' ? 'hủy đơn' : reasonAction === 'reject' ? 'từ chối' : 'yêu cầu sửa'}</label>
            <textarea id="order-action-reason" required maxLength={300} value={reason} onChange={e => setReason(e.target.value)} />
          </div>
          <div className="form-actions">
            <Button disabled={action.isPending || !reason.trim() || (reasonAction === 'approve' && (!credit.data || credit.isError || credit.isFetching || !profile.permissions.includes('Sales.OverrideCreditLimit')))}>Xác nhận</Button>
            <Button type="button" variant="outline" onClick={() => { setReasonAction(''); setReason('') }}>Đóng</Button>
          </div>
        </form>}
        {order.expectedDate && <p>Ngày dự kiến: {calendarDate(order.expectedDate)} {order.isOverdue && <span className="badge badge-warn">Quá hạn</span>}</p>}
        {query.data.note && <p className="muted section-gap">{query.data.note}</p>}
      </section>
      {canViewCredit && <CreditExposurePanel query={credit} showProjection={[1, 2, 8].includes(order.status)} />}
      {order.status === 2 && kind === 2 && credit.data?.requiresOverride && <p className="notice notice-error">Đơn vượt hạn mức công nợ. Cần quyền duyệt vượt hạn mức và lý do để tiếp tục.</p>}
      <div className="table-panel">
        <div className="table-scroll" role="region" aria-label="Dòng đơn hàng" tabIndex={0}>
          <table aria-label="Dòng đơn hàng">
            <thead>
              <tr>
                <th>Sản phẩm</th>
                <th>Đặt hàng</th>
                <th>Đã {kind === 1 ? 'nhận' : 'giao'}</th>
                <th>Đã trả</th>
                <th>Đang giữ</th>
                <th>Đơn giá (VND)</th>
              </tr>
            </thead>
            <tbody>
              {lines.map((l) => (
                <tr key={l.productId}>
                  <td>
                    {profile.permissions.includes('Catalog.View') ? (
                      <Link className="record-link" to={'/san-pham/' + l.productId}>
                        {l.sku}
                      </Link>
                    ) : (
                      <strong>{l.sku}</strong>
                    )}
                    <div>{l.productName}</div>
                  </td>
                  <td>{money(l.quantity)}</td>
                  <td>{money(l.fulfilledQuantity)}</td>
                  <td>{money(l.returnedQuantity)}</td>
                  <td>{money(l.reservedQuantity)}</td>
                  <td className="number">{money(l.unitPrice)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
      {profile.permissions.includes('Inventory.Post') && [3, 4, 5, 6].includes(order.status) && (
        <section className="panel section-gap">
          <h2>{returning ? 'Trả hàng' : kind === 1 ? 'Nhận hàng' : 'Giao hàng'}</h2>
          <p className="page-description">
            Chỉ nhập số lượng thực hiện đợt này. Ghi sổ cập nhật kho và hóa đơn nội bộ cùng giao
            dịch.
          </p>
          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={returning}
              disabled={order.status >= 5}
              onChange={(e) => {
                setReturn(e.target.checked)
                setQuantities({})
                setRequestId(crypto.randomUUID())
              }}
            />{' '}
            Lập phiếu trả hàng
          </label>
          <form
            className="form-stack"
            onSubmit={(e) => {
              e.preventDefault()
              const selected = lines
                .map((l) => ({
                  productId: l.productId,
                  quantity: Number(quantities[l.productId] || 0),
                }))
                .filter((l) => l.quantity > 0)
              if (selected.length)
                post(
                  '/fulfill',
                  { requestId, isReturn: returning, lines: selected },
                  'Ghi sổ số lượng đã nhập?',
                )
            }}
          >
            <fieldset className="form-stack" disabled={action.isPending}>
              <div className="form-grid">
                {lines.map((l) => {
                  const max = returning
                    ? l.fulfilledQuantity - l.returnedQuantity
                    : l.quantity - l.fulfilledQuantity
                  return (
                    max > 0 && (
                      <div className="field" key={l.productId}>
                        <label htmlFor={'fulfill-' + l.productId}>
                          {l.sku} · {l.productName}{' '}
                          <span className="muted">(tối đa {money(max)})</span>
                        </label>
                        <input
                          id={'fulfill-' + l.productId}
                          type="number"
                          min="0"
                          max={max}
                          step="0.0001"
                          value={quantities[l.productId] ?? ''}
                          placeholder="0"
                          onChange={(e) => {
                            setQuantities({ ...quantities, [l.productId]: e.target.value })
                            setRequestId(crypto.randomUUID())
                          }}
                        />
                      </div>
                    )
                  )
                })}
              </div>
              <div className="form-actions">
                <Button
                  disabled={
                    action.isPending || !Object.values(quantities).some((n) => Number(n) > 0)
                  }
                >
                  Ghi sổ {returning ? 'trả hàng' : kind === 1 ? 'nhận hàng' : 'giao hàng'}
                </Button>
              </div>
            </fieldset>
          </form>
        </section>
      )}
      <DocumentTimeline title="Lịch sử phê duyệt" history={query.data.history} />
      <section className="panel section-gap">
        <h2>Lịch sử thực hiện</h2>
        {fulfillments.length === 0 ? (
          <p className="muted">{[6, 7].includes(order.status) ? 'Đơn đã kết thúc và chưa có phiếu thực hiện.' : order.status === 8 ? 'Chưa có phiếu thực hiện. Sửa đơn rồi gửi duyệt lại.' : 'Chưa có phiếu thực hiện. Duyệt đơn để bắt đầu.'}</p>
        ) : (
          <ol className="document-timeline" aria-label="Lịch sử thực hiện">
            {fulfillments.map((f) => (
              <li key={f.id}>
                <div>
                  <strong>
                    {f.reversesId
                      ? 'Đảo giao dịch'
                      : f.isReturn
                        ? 'Trả hàng'
                        : kind === 1
                          ? 'Nhận hàng'
                          : 'Giao hàng'}
                  </strong>
                  <small>{date(f.createdAtUtc)}</small>
                </div>
                {profile.permissions.includes('Inventory.View') ? (
                  <Link className="record-link" to={'/ton-kho/chung-tu/' + f.stockDocumentId}>
                    Phiếu kho #{f.stockDocumentId}
                  </Link>
                ) : (
                  <span>Phiếu kho #{f.stockDocumentId}</span>
                )}
                <span className="number">{money(f.amount)} VND</span>
                {profile.permissions.includes('Inventory.Post') &&
                  !f.reversesId &&
                  !fulfillments.some((x) => x.reversesId === f.id) && (
                    <Button
                      variant="outline"
                      disabled={action.isPending}
                      onClick={() =>
                        post(
                          '/fulfillments/' + f.id + '/reverse',
                          { requestId: crypto.randomUUID() },
                          'Đảo giao dịch kho và hóa đơn liên quan?',
                        )
                      }
                    >
                      Đảo giao dịch
                    </Button>
                  )}
              </li>
            ))}
          </ol>
        )}
      </section>
    </>
  )
}

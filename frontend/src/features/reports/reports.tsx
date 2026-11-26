import { useQuery } from '@tanstack/react-query'
import { ArrowUpRight, Bell, Search } from 'lucide-react'
import { useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { DataTable } from '../../components/data-table'
import { EmptyState, ErrorNotice, Loading } from '../../components/feedback'
import { api, type Page, type Profile } from '../../lib/api'
import { useDebouncedValue } from '../../lib/use-debounced-value'
import { useLookupOptions } from '../catalog/listing'
import { businessDate, calendarDate, date, money } from '../../lib/format'
import { StockReconciliation } from './stock-reconciliation'

type Dashboard = {
  metrics: { key: string; label: string; value: number; unit: string; href: string }[]
  trend: { day: string; receiptCount: number; issueCount: number }[]
  profitTrend: { day: string; revenue: number; cost: number; grossProfit: number }[]
  topProducts: { productId: string; sku: string; name: string; quantity: number; revenue: number }[]
  alerts: { label: string; count: number; href: string }[]
}
type Stock = {
  warehouse: string
  productId: string
  sku: string
  name: string
  opening: number
  received: number
  issued: number
  closing: number
  lastMovementAtUtc: string | null
  minimumStock: number
}
type Audit = {
  id: number
  timestampUtc: string
  actor: string
  action: string
  entity: string
  entityId: string
  result: string
  correlationId: string
}
const kindLabels: Record<string, string> = {
  product: 'Sản phẩm',
  sales: 'Đơn bán',
  purchase: 'Đơn mua',
  invoice: 'Hóa đơn',
  stock: 'Phiếu kho',
  customer: 'Khách hàng',
  supplier: 'Nhà cung cấp',
}
const auditActions: Record<string, string> = {
  CreateOrder: 'Lập đơn hàng',
  submitOrder: 'Gửi duyệt đơn',
  approveOrder: 'Duyệt đơn hàng',
  cancelOrder: 'Hủy đơn hàng',
  FulfillOrder: 'Thực hiện đơn hàng',
  ReturnOrder: 'Trả hàng',
  ReverseFulfillment: 'Đảo giao dịch đơn',
  CreateStockDocument: 'Lập phiếu kho',
  PostStockDocument: 'Ghi sổ phiếu kho',
  CancelStockDocument: 'Hủy phiếu kho',
  ReverseStockDocument: 'Đảo phiếu kho',
  CreateProduct: 'Tạo sản phẩm',
  UpdateProduct: 'Sửa sản phẩm',
  CreatePartner: 'Tạo đối tác',
  UpdatePartner: 'Sửa đối tác',
  CreateLookup: 'Tạo danh mục',
  UpdateLookup: 'Sửa danh mục',
  PostPayment: 'Ghi sổ thanh toán',
  ImportProducts: 'Nhập sản phẩm từ Excel',
  Login: 'Đăng nhập',
  Logout: 'Đăng xuất',
  'Finance.Payment': 'Ghi sổ thanh toán',
  'Finance.Refund': 'Hoàn tiền',
}
const entityLabels: Record<string, string> = {
  Order: 'Đơn hàng',
  Product: 'Sản phẩm',
  Customer: 'Khách hàng',
  Supplier: 'Nhà cung cấp',
  StockDocument: 'Phiếu kho',
  Payment: 'Thanh toán',
  User: 'Người dùng',
  Role: 'Vai trò',
  Warehouse: 'Kho',
  Unit: 'Đơn vị tính',
  Category: 'Nhóm hàng',
  PaymentMethod: 'Phương thức thanh toán',
}
export function usePeriod(initial?: { from: string | null; to: string | null }) {
  const [from, setFrom] = useState(() => initial?.from || businessDate().slice(0, 8) + '01')
  const [to, setTo] = useState(() => initial?.to || businessDate())
  return { from, to, setFrom, setTo, parameters: new URLSearchParams({ from, to }).toString() }
}
function Period({
  period,
  onChange,
}: {
  period: ReturnType<typeof usePeriod>
  onChange?: () => void
}) {
  return (
    <div className="period-filter">
      <div className="field">
        <label htmlFor="period-from">Từ ngày</label>
        <input
          id="period-from"
          type="date"
          max={period.to}
          value={period.from}
          required
          onChange={(e) => {
            if (e.target.value) {
              period.setFrom(e.target.value)
              onChange?.()
            }
          }}
        />
      </div>
      <div className="field">
        <label htmlFor="period-to">Đến ngày</label>
        <input
          id="period-to"
          type="date"
          min={period.from}
          value={period.to}
          required
          onChange={(e) => {
            if (e.target.value) {
              period.setTo(e.target.value)
              onChange?.()
            }
          }}
        />
      </div>
    </div>
  )
}
export function DashboardPage({ profile }: { profile: Profile }) {
  const period = usePeriod()
  const [chartDay, setChartDay] = useState('')
  const query = useQuery({
    queryKey: ['dashboard', period.parameters],
    queryFn: ({ signal }) => api<Dashboard>('/reports/dashboard?' + period.parameters, { signal }),
  })
  const chartPoints = query.data?.trend.slice(-14) ?? []
  const chartMaximum = Math.max(
    1,
    ...chartPoints.map((point) => Math.max(point.receiptCount, point.issueCount)),
  )
  const selectedPoint = chartPoints.find((point) => point.day === chartDay) ?? chartPoints.at(-1)
  const today = new Date().toLocaleDateString('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  })
  return (
    <>
      <header className="page-heading">
        <div>
          <h1>Tổng quan hoạt động</h1>
          <p className="page-description">
            {today} · Xin chào {profile.displayName}
          </p>
        </div>
        <Period period={period} />
      </header>
      <ErrorNotice error={query.error} />
      {query.isPending ? (
        <Loading />
      ) : (
        query.data && (
          <>
            <section className="metrics-strip" aria-label="Chỉ số hoạt động">
              {query.data.metrics.map((m) => (
                <a href={m.href} key={m.key}>
                  <span>
                    {m.label}
                    <ArrowUpRight size={15} aria-hidden />
                  </span>
                  <strong>
                    {money(m.value)}
                    <small>{m.unit}</small>
                  </strong>
                </a>
              ))}
            </section>
            <div className="dashboard-grid">
              <section className="panel dashboard-priority">
                <div className="section-heading">
                  <Bell size={19} aria-hidden />
                  <h2>Cần xử lý</h2>
                </div>
                {query.data.alerts.length ? (
                  <ul className="work-queue">
                    {query.data.alerts.map((a) => (
                      <li key={a.label}>
                        <Link to={a.href}>
                          <span>{a.label}</span>
                          <strong>{money(a.count)}</strong>
                          <ArrowUpRight size={16} aria-hidden />
                        </Link>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <EmptyState>Không có cảnh báo trong phạm vi quyền của bạn.</EmptyState>
                )}
                <p className="muted report-footnote">
                  Mở từng mục để đối chiếu chứng từ và số dư liên quan.
                </p>
              </section>
              {profile.permissions.includes('Inventory.View') && (
                <section className="panel">
                  <h2>Phiếu nhập · xuất theo ngày</h2>
                  <p className="page-description">
                    Đếm phiếu nhập và xuất đã ghi sổ ban đầu, kể cả phiếu được đảo sau đó; không gồm
                    chuyển kho, điều chỉnh hoặc phiếu đảo. Tối đa 14 ngày có phát sinh gần nhất.
                  </p>
                  {query.data.trend.length ? (
                    <div
                      className="movement-chart"
                      role="group"
                      aria-label="Số phiếu nhập và phiếu xuất theo ngày"
                    >
                      <span className="chart-scale" aria-hidden="true">
                        <span>{money(chartMaximum)}</span>
                        <span>{money(chartMaximum / 2)}</span>
                        <span>0</span>
                      </span>
                      {chartPoints.map((d) => {
                        const label = `${calendarDate(d.day)}: ${money(d.receiptCount)} phiếu nhập, ${money(d.issueCount)} phiếu xuất`
                        return (
                          <button
                            type="button"
                            className="chart-day"
                            key={d.day}
                            title={label}
                            aria-label={label}
                            aria-pressed={selectedPoint?.day === d.day}
                            onClick={() => setChartDay(d.day)}
                            onFocus={() => setChartDay(d.day)}
                          >
                            <span className="chart-bars" aria-hidden="true">
                              <span
                                style={{ height: `${(d.receiptCount / chartMaximum) * 100}%` }}
                              />
                              <span style={{ height: `${(d.issueCount / chartMaximum) * 100}%` }} />
                            </span>
                            <small>{Number(businessDate(d.day).slice(-2))}</small>
                          </button>
                        )
                      })}
                    </div>
                  ) : (
                    <EmptyState>Chưa có biến động kho trong kỳ này.</EmptyState>
                  )}
                  <div className="chart-legend">
                    <span>Phiếu nhập</span>
                    <span>Phiếu xuất</span>
                  </div>
                  {selectedPoint && (
                    <output className="chart-readout" aria-live="polite">
                      {calendarDate(selectedPoint.day)} · {money(selectedPoint.receiptCount)} phiếu
                      nhập · {money(selectedPoint.issueCount)} phiếu xuất
                    </output>
                  )}
                  <details className="chart-data">
                    <summary>Xem số liệu biểu đồ</summary>
                    <div className="table-scroll">
                      <table>
                        <thead>
                          <tr>
                            <th>Ngày</th>
                            <th>Phiếu nhập</th>
                            <th>Phiếu xuất</th>
                          </tr>
                        </thead>
                        <tbody>
                          {query.data.trend.map((t) => (
                            <tr key={t.day}>
                              <td>{calendarDate(t.day)}</td>
                              <td>{money(t.receiptCount)}</td>
                              <td>{money(t.issueCount)}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </details>
                </section>
              )}
            </div>
            {profile.permissions.includes('Reports.Sensitive') &&
              profile.permissions.includes('Sales.View') && (
                <section id="financial-report" className="panel section-gap">
                  <h2>Doanh thu, giá vốn & lợi nhuận</h2>
                  <p className="page-description">
                    Số liệu theo ngày ghi sổ, gồm điều chỉnh trả hàng và đảo giao dịch.
                  </p>
                  <details className="chart-data">
                    <summary>Xem đối chiếu theo ngày</summary>
                    <DataTable
                      label="Doanh thu và lợi nhuận theo ngày"
                      page={{
                        items: query.data.profitTrend,
                        total: query.data.profitTrend.length,
                        pageNumber: 1,
                        pageSize: Math.max(1, query.data.profitTrend.length),
                      }}
                      onPage={() => {}}
                      columns={[
                        {
                          accessorKey: 'day',
                          header: 'Ngày',
                          cell: ({ row }) => calendarDate(row.original.day),
                        },
                        ...(['revenue', 'cost', 'grossProfit'] as const).map((key, i) => ({
                          accessorKey: key,
                          header: ['Doanh thu (VND)', 'Giá vốn (VND)', 'Lợi nhuận gộp (VND)'][i],
                          cell: ({
                            row,
                          }: {
                            row: { original: Dashboard['profitTrend'][number] }
                          }) => money(row.original[key]),
                        })),
                      ]}
                    />
                  </details>
                </section>
              )}
            {query.data.topProducts.length > 0 && (
              <section className="section-gap">
                <header className="section-heading section-heading-between">
                  <h2>Sản phẩm bán chạy</h2>
                </header>
                <DataTable
                  label="Sản phẩm bán chạy"
                  page={{
                    items: query.data.topProducts,
                    total: query.data.topProducts.length,
                    pageNumber: 1,
                    pageSize: 10,
                  }}
                  onPage={() => {}}
                  columns={[
                    {
                      accessorKey: 'sku',
                      header: 'SKU',
                      cell: ({ row }) =>
                        profile.permissions.includes('Catalog.View') ? (
                          <Link className="record-link" to={'/san-pham/' + row.original.productId}>
                            {row.original.sku}
                          </Link>
                        ) : (
                          row.original.sku
                        ),
                    },
                    { accessorKey: 'name', header: 'Sản phẩm' },
                    {
                      accessorKey: 'quantity',
                      header: 'Số lượng',
                      cell: ({ row }) => money(row.original.quantity),
                    },
                    {
                      accessorKey: 'revenue',
                      header: 'Doanh thu (VND)',
                      cell: ({ row }) => money(row.original.revenue),
                    },
                  ]}
                />
              </section>
            )}
          </>
        )
      )}
    </>
  )
}

export function ReportsPage({ profile }: { profile: Profile }) {
  const [params] = useSearchParams()
  const currentDiagnostics = params.get('view') === 'reconciliation'
  const period = usePeriod({ from: params.get('from'), to: params.get('to') })
  const [page, setPage] = useState(1)
  const [warehouse, setWarehouse] = useState('')
  const [filter, setFilter] = useState(
    params.get('filter') === 'low' || params.get('filter') === 'slow'
      ? params.get('filter')!
      : 'all',
  )
  const warehouses = useLookupOptions(
    'warehouses',
    undefined,
    profile.permissions.includes('Catalog.View'),
  )
  const query = useQuery({
    queryKey: ['report-stock', period.parameters, page, warehouse, filter],
    queryFn: ({ signal }) =>
      api<Page<Stock>>(
        `/reports/stock?${period.parameters}&page=${page}&pageSize=20${warehouse ? '&warehouseId=' + encodeURIComponent(warehouse) : ''}&filter=${filter}`,
        { signal },
      ),
    enabled: !currentDiagnostics,
  })
  return (
    <>
      <header className="page-heading">
        <div>
          <h1>{currentDiagnostics ? 'Đối soát tồn kho' : 'Báo cáo nhập xuất tồn'}</h1>
          <p className="page-description">
            {currentDiagnostics
              ? 'Kiểm tra hàng đang có và hàng đã giữ theo từng kho, từng SKU.'
              : 'Đối chiếu số dư đầu kỳ, luân chuyển và cuối kỳ từ sổ kho theo ngày Việt Nam.'}
          </p>
        </div>
        {!currentDiagnostics && <Period period={period} onChange={() => setPage(1)} />}
      </header>
      <nav className="action-row reconciliation-note" aria-label="Chế độ báo cáo kho">
        <Link
          className={'button ' + (!currentDiagnostics ? 'button-primary' : 'button-outline')}
          aria-current={!currentDiagnostics ? 'page' : undefined}
          to={'/bao-cao?' + period.parameters}
        >
          Báo cáo theo kỳ
        </Link>
        <Link
          className={'button ' + (currentDiagnostics ? 'button-primary' : 'button-outline')}
          aria-current={currentDiagnostics ? 'page' : undefined}
          to={'/bao-cao?' + period.parameters + '&view=reconciliation'}
        >
          Đối soát hiện tại
        </Link>
      </nav>
      <div className="table-toolbar">
        <label className="filter-select">
          <span className="sr-only">Kho báo cáo</span>
          <select
            aria-label="Kho báo cáo"
            value={warehouse}
            disabled={!profile.permissions.includes('Catalog.View')}
            onChange={(e) => {
              setWarehouse(e.target.value)
              setPage(1)
            }}
          >
            <option value="">Tất cả kho</option>
            {warehouses.data?.map((w) => (
              <option key={w.id} value={w.id}>
                {w.name}
              </option>
            ))}
          </select>
        </label>
        {!currentDiagnostics && (
          <label className="filter-select">
            <span className="sr-only">Tình trạng hàng tồn</span>
            <select
              value={filter}
              onChange={(e) => {
                setFilter(e.target.value)
                setPage(1)
              }}
            >
              <option value="all">Tất cả hàng hóa</option>
              <option value="low">Dưới định mức</option>
              <option value="slow">Không luân chuyển 90 ngày</option>
            </select>
          </label>
        )}
        {profile.permissions.includes('Finance.View') && (
          <Link className="button button-outline" to="/tai-chinh">
            Báo cáo công nợ
          </Link>
        )}
      </div>
      <ErrorNotice error={(currentDiagnostics ? null : query.error) ?? warehouses.error} />
      {currentDiagnostics ? (
        <StockReconciliation key={warehouse} warehouseId={warehouse} />
      ) : query.isPending ? (
        <Loading />
      ) : (
        query.data && (
          <DataTable
            label="Nhập xuất tồn"
            page={query.data}
            onPage={setPage}
            hiddenColumns
            columns={[
              {
                accessorKey: 'sku',
                header: 'Sản phẩm',
                cell: ({ row }) => (
                  <div className="table-identity">
                    {profile.permissions.includes('Inventory.View') ? (
                      <Link
                        className="record-link"
                        to={'/ton-kho/lich-su?productId=' + row.original.productId}
                      >
                        {row.original.sku}
                      </Link>
                    ) : (
                      <strong>{row.original.sku}</strong>
                    )}
                    <span>{row.original.name}</span>
                  </div>
                ),
              },
              { accessorKey: 'warehouse', header: 'Kho' },
              ...(['opening', 'received', 'issued', 'closing', 'minimumStock'] as const).map(
                (key, i) => ({
                  accessorKey: key,
                  header: ['Đầu kỳ', 'Nhập', 'Xuất', 'Cuối kỳ', 'Định mức'][i],
                  cell: ({ row }: { row: { original: Stock } }) => (
                    <span className="number">{money(row.original[key])}</span>
                  ),
                }),
              ),
              {
                accessorKey: 'lastMovementAtUtc',
                header: 'Luân chuyển gần nhất',
                cell: ({ row }) =>
                  row.original.lastMovementAtUtc
                    ? date(row.original.lastMovementAtUtc)
                    : 'Chưa phát sinh',
              },
            ]}
          />
        )
      )}
    </>
  )
}

export function AuditPage() {
  const period = usePeriod()
  const [text, setText] = useState('')
  const search = useDebouncedValue(text)
  const [page, setPage] = useState(1)
  const query = useQuery({
    queryKey: ['audit', period.parameters, page, search],
    queryFn: ({ signal }) =>
      api<Page<Audit>>(
        `/audit?${period.parameters}&page=${page}&pageSize=20&search=${encodeURIComponent(search)}`,
        { signal },
      ),
  })
  return (
    <>
      <header className="page-heading">
        <div>
          <h1>Nhật ký hệ thống</h1>
          <p className="page-description">Theo dõi thao tác và mã đối chiếu để truy vết.</p>
        </div>
        <Period period={period} onChange={() => setPage(1)} />
      </header>
      <div className="table-toolbar">
        <label className="search-box">
          <Search size={17} aria-hidden />
          <span className="sr-only">Tìm nhật ký</span>
          <input
            type="search"
            maxLength={100}
            value={text}
            placeholder="Tìm thao tác, đối tượng…"
            onChange={(e) => {
              setText(e.target.value)
              setPage(1)
            }}
          />
        </label>
      </div>
      <ErrorNotice error={query.error} />
      {query.isPending ? (
        <Loading />
      ) : (
        query.data && (
          <DataTable
            label="Nhật ký hệ thống"
            page={query.data}
            onPage={setPage}
            hiddenColumns
            columns={[
              {
                accessorKey: 'timestampUtc',
                header: 'Thời gian',
                cell: ({ row }) => date(row.original.timestampUtc),
              },
              { accessorKey: 'actor', header: 'Người thực hiện' },
              {
                accessorKey: 'action',
                header: 'Thao tác',
                cell: ({ row }) => (
                  <span title={row.original.action}>
                    {auditActions[row.original.action] ?? row.original.action}
                  </span>
                ),
              },
              {
                accessorKey: 'entity',
                header: 'Đối tượng',
                cell: ({ row }) => (
                  <div className="table-identity">
                    {entityLabels[row.original.entity] ?? row.original.entity}
                    <span>{row.original.entityId}</span>
                  </div>
                ),
              },
              {
                accessorKey: 'result',
                header: 'Kết quả',
                cell: ({ row }) =>
                  row.original.result === 'Success' || row.original.result === 'Succeeded'
                    ? 'Thành công'
                    : row.original.result === 'Failure' || row.original.result === 'Failed'
                      ? 'Thất bại'
                      : row.original.result,
              },
              { accessorKey: 'correlationId', header: 'Mã đối chiếu' },
            ]}
          />
        )
      )}
    </>
  )
}

export function SearchPage() {
  const [params, setParams] = useSearchParams()
  const [text, setText] = useState(params.get('q') ?? '')
  const q = useDebouncedValue(text)
  const query = useQuery({
    queryKey: ['global-search', q],
    queryFn: ({ signal }) =>
      api<{ kind: string; label: string; detail: string; href: string }[]>(
        '/search?q=' + encodeURIComponent(q),
        { signal },
      ),
    enabled: q.trim().length >= 2,
  })
  return (
    <>
      <header className="page-heading">
        <div>
          <h1>Tìm kiếm toàn hệ thống</h1>
          <p className="page-description">
            Tìm sản phẩm, đối tác và chứng từ trong phạm vi quyền của bạn.
          </p>
        </div>
      </header>
      <label className="search-box global-search">
        <Search size={17} aria-hidden />
        <span className="sr-only">Nội dung tìm kiếm</span>
        <input
          autoFocus
          type="search"
          maxLength={100}
          placeholder="Nhập ít nhất 2 ký tự…"
          value={text}
          onChange={(e) => {
            setText(e.target.value)
            setParams({ q: e.target.value }, { replace: true })
          }}
        />
      </label>
      <ErrorNotice error={query.error} />
      {q.trim().length < 2 ? (
        <EmptyState>Nhập mã chứng từ, SKU hoặc tên đối tác để bắt đầu.</EmptyState>
      ) : query.isPending ? (
        <Loading />
      ) : query.data?.length ? (
        <ul className="search-results">
          {query.data.map((r, i) => (
            <li key={i}>
              <Link to={r.href}>
                <span className="badge badge-neutral">{kindLabels[r.kind] ?? r.kind}</span>
                <div>
                  <strong>{r.label}</strong>
                  <p>{r.detail}</p>
                </div>
                <ArrowUpRight size={18} aria-hidden />
              </Link>
            </li>
          ))}
        </ul>
      ) : (
        <EmptyState>Không có kết quả phù hợp. Thử mã hoặc tên khác.</EmptyState>
      )}
    </>
  )
}

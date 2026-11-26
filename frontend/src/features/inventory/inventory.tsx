import { keepPreviousData, useMutation, useQuery } from '@tanstack/react-query'
import type { ColumnDef } from '@tanstack/react-table'
import { ArrowLeft, ArrowRight, Plus, Search, Printer } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router'
import { DataTable } from '../../components/data-table'
import { ErrorNotice, Loading } from '../../components/feedback'
import { Button } from '../../components/ui/button'
import { api, type Page, type Profile } from '../../lib/api'
import { formatMoney, formatQuantity } from '../../lib/labels'
import { queryClient } from '../../lib/query'
import { useDebouncedValue } from '../../lib/use-debounced-value'
import { useLookupOptions } from '../catalog/listing'

const root = '/inventory'
const docPath = '/ton-kho/chung-tu'
const time = new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' })
const kindLabels: Record<number, string> = {
  1: 'Nhập kho', 2: 'Xuất kho', 3: 'Chuyển kho', 4: 'Điều chỉnh', 5: 'Kiểm kê', 6: 'Đảo chứng từ',
}
const statusLabels: Record<number, string> = { 1: 'Nháp', 2: 'Đã ghi sổ', 3: 'Đã hủy', 4: 'Đã đảo' }
type Balance = { warehouseId: string; warehouseName: string; productId: string; sku: string; productName: string;
  onHand: number; reserved: number; available: number; averageCost: number; minimumStock: number; isLowStock: boolean }
type Movement = { id: number; documentId: number; documentNumber: string; warehouseId: string;
  warehouseName: string; productId: string; sku: string; quantityDelta: number; unitCost: number;
  balanceAfter: number; postedAt: string }
export type StockDocument = { id: number; number: string; kind: number; status: number; warehouseId: string;
  warehouseName: string; destinationName: string | null; createdAt: string; postedAt: string | null; reversesId: number | null }
type StockLine = { productId: string; sku: string; productName: string; quantity: number; unitCost: number | null }
export type StockDetail = { document: StockDocument; note: string | null; destinationId: string | null; lines: StockLine[] }

function warehouseQuery(warehouse: string, page: number) {
  const params = new URLSearchParams({ page: String(page), pageSize: '15' })
  if (warehouse) params.set('warehouseId', warehouse)
  return params
}

function WarehouseFilter({ value, onChange }: { value: string; onChange: (warehouse: string) => void }) {
  const warehouses = useLookupOptions('warehouses')
  return <label className="filter-select"><span className="sr-only">Lọc kho</span>
    <select value={value} onChange={event => onChange(event.target.value)}>
      <option value="">Tất cả kho</option>
      {warehouses.data?.map(item => <option value={item.id} key={item.id}>{item.name}</option>)}
    </select>
  </label>
}

const balanceColumns: ColumnDef<Balance>[] = [
  { accessorKey: 'sku', header: 'SKU', cell: ({ row }) =>
    <Link className="record-link whitespace-nowrap" to={'/san-pham/' + row.original.productId}>{row.original.sku}</Link>, enableHiding: false },
  { accessorKey: 'productName', header: 'Sản phẩm', enableHiding: false },
  { accessorKey: 'warehouseName', header: 'Kho' },
  { accessorKey: 'onHand', header: 'Tồn thực tế', cell: ({ row }) => formatQuantity(row.original.onHand) },
  { accessorKey: 'reserved', header: 'Đang giữ', cell: ({ row }) => formatQuantity(row.original.reserved) },
  { accessorKey: 'available', header: 'Khả dụng', cell: ({ row }) => <strong className="number">{formatQuantity(row.original.available)}</strong> },
  { accessorKey: 'averageCost', header: 'Giá vốn BQ', cell: ({ row }) => formatMoney(row.original.averageCost) },
  { id: 'status', header: 'Định mức', cell: ({ row }) =>
    <span className={'badge ' + (row.original.isLowStock ? 'badge-warn' : 'badge-good')}>
      {row.original.isLowStock ? 'Dưới định mức' : 'Đạt'}
    </span> },
]

export function InventoryPage() {
  const [page, setPage] = useState(1)
  const [warehouse, setWarehouse] = useState('')
  const [term, setTerm] = useState('')
  const [low, setLow] = useState(false)
  const search = useDebouncedValue(term)
  const params = warehouseQuery(warehouse, page)
  if (search) params.set('search', search)
  if (low) params.set('lowOnly', 'true')

  const inventory = useQuery({
    queryKey: ['inventory', 'balances', params.toString()],
    queryFn: ({ signal }) => api<Page<Balance>>(root + '/balances?' + params, { signal }),
    placeholderData: keepPreviousData,
  })
  return <>
    <header className="page-heading">
      <div><h1>Tồn kho</h1><p className="page-description">Theo dõi tồn, hàng đang giữ và hàng dưới định mức theo kho.</p></div>
      <Button asChild variant="outline"><Link to={docPath}>Chứng từ kho <ArrowRight size={16} aria-hidden /></Link></Button>
    </header>
    <div className="table-toolbar catalog-toolbar">
      <label className="search-box"><Search size={17} aria-hidden /><span className="sr-only">Tìm tồn kho</span>
        <input type="search" maxLength={120} value={term} placeholder="Tìm theo SKU hoặc tên"
          onChange={event => { setTerm(event.target.value); setPage(1) }} />
      </label>
      <WarehouseFilter value={warehouse} onChange={value => { setWarehouse(value); setPage(1) }} />
      <label className="checkbox-row"><input type="checkbox" checked={low} onChange={event => {
        setLow(event.target.checked); setPage(1)
      }} /> Dưới định mức</label>
    </div>
    <ErrorNotice error={inventory.error} />
    {inventory.isPending ? <Loading /> : inventory.data &&
      <DataTable page={inventory.data} columns={balanceColumns} label="Tồn kho theo sản phẩm"
        onPage={setPage} hiddenColumns />}
  </>
}

const movementColumns: ColumnDef<Movement>[] = [
  { accessorKey: 'documentNumber', header: 'Chứng từ', cell: ({ row }) =>
    <Link className="record-link" to={docPath + '/' + row.original.documentId}>{row.original.documentNumber}</Link> },
  { accessorKey: 'postedAt', header: 'Ngày ghi sổ', cell: ({ row }) => time.format(new Date(row.original.postedAt)) },
  { accessorKey: 'warehouseName', header: 'Kho' },
  { accessorKey: 'sku', header: 'SKU' },
  { accessorKey: 'quantityDelta', header: 'Biến động', cell: ({ row }) => <strong className="number">
    {row.original.quantityDelta > 0 ? '+' : ''}{formatQuantity(row.original.quantityDelta)}
  </strong> },
  { accessorKey: 'unitCost', header: 'Giá vốn', cell: ({ row }) => formatMoney(row.original.unitCost) },
  { accessorKey: 'balanceAfter', header: 'Tồn sau ghi sổ', cell: ({ row }) => formatQuantity(row.original.balanceAfter) },
]

export function HistoryPage() {
  const [searchParams] = useSearchParams()
  const [page, setPage] = useState(1)
  const [warehouse, setWarehouse] = useState('')
  const params = warehouseQuery(warehouse, page)
  const productId = searchParams.get('productId')
  if (productId) params.set('productId', productId)
  const query = useQuery({
    queryKey: ['inventory', 'history', params.toString()],
    queryFn: ({ signal }) => api<Page<Movement>>(root + '/history?' + params, { signal }),
    placeholderData: keepPreviousData,
  })
  return <>
    <header className="page-heading">
      <div><h1>Biến động kho</h1><p className="page-description">Tra cứu biến động và giá vốn theo chứng từ đã ghi sổ.</p></div>
    </header>
    <div className="table-toolbar"><WarehouseFilter value={warehouse} onChange={value => { setWarehouse(value); setPage(1) }} />{productId && <Link className="record-link" to="/ton-kho/lich-su">Bỏ lọc sản phẩm</Link>}</div>
    <ErrorNotice error={query.error} />
    {query.isPending ? <Loading /> : query.data &&
      <DataTable page={query.data} columns={movementColumns} label="Lịch sử biến động kho" onPage={setPage} />}
  </>
}

const statusBadge = (status: number) => <span className={'badge ' + (status === 2 ? 'badge-good' : 'badge-warn')}>
  {statusLabels[status]}
</span>
const documentColumns: ColumnDef<StockDocument>[] = [
  { accessorKey: 'number', header: 'Số chứng từ', cell: ({ row }) =>
    <Link className="record-link" to={docPath + '/' + row.original.id}>{row.original.number}</Link> },
  { accessorKey: 'kind', header: 'Loại phiếu', cell: ({ row }) => kindLabels[row.original.kind] },
  { accessorKey: 'warehouseName', header: 'Kho', cell: ({ row }) => <div className="table-identity">
    <strong>{row.original.warehouseName}</strong>{row.original.destinationName && <span>Đến {row.original.destinationName}</span>}
  </div> },
  { accessorKey: 'createdAt', header: 'Ngày lập', cell: ({ row }) => time.format(new Date(row.original.createdAt)) },
  { accessorKey: 'status', header: 'Trạng thái', cell: ({ row }) => statusBadge(row.original.status) },
]

export function DocumentsPage({ profile }: { profile: Profile }) {
  const [page, setPage] = useState(1)
  const [warehouse, setWarehouse] = useState('')
  const [kind, setKind] = useState('')
  const [status, setStatus] = useState('')
  const params = warehouseQuery(warehouse, page)
  if (kind) params.set('kind', kind)
  if (status) params.set('status', status)
  const query = useQuery({
    queryKey: ['inventory', 'documents', params.toString()],
    queryFn: ({ signal }) => api<Page<StockDocument>>(root + '/documents?' + params, { signal }),
    placeholderData: keepPreviousData,
  })
  return <>
    <header className="page-heading">
      <div><h1>Chứng từ kho</h1><p className="page-description">Lập và ghi sổ phiếu nhập, xuất, chuyển, điều chỉnh hoặc kiểm kê.</p></div>
      {profile.permissions.includes('Inventory.Post') && <Button asChild><Link to={docPath + '/moi'}>
        <Plus size={17} aria-hidden /> Lập phiếu
      </Link></Button>}
    </header>
    <div className="table-toolbar catalog-toolbar">
      <label className="filter-select"><span className="sr-only">Lọc loại phiếu</span>
        <select value={kind} onChange={event => { setKind(event.target.value); setPage(1) }}>
          <option value="">Tất cả loại phiếu</option>
          {Object.entries(kindLabels).map(([id, label]) => <option key={id} value={id}>{label}</option>)}
        </select>
      </label>
      <WarehouseFilter value={warehouse} onChange={value => { setWarehouse(value); setPage(1) }} />
      <label className="filter-select"><span className="sr-only">Lọc trạng thái phiếu</span>
        <select value={status} onChange={event => { setStatus(event.target.value); setPage(1) }}>
          <option value="">Tất cả trạng thái</option>
          {Object.entries(statusLabels).map(([id, label]) => <option key={id} value={id}>{label}</option>)}
        </select>
      </label>
    </div>
    <ErrorNotice error={query.error} />
    {query.isPending ? <Loading /> : query.data &&
      <DataTable page={query.data} columns={documentColumns} label="Chứng từ kho" onPage={setPage} />}
  </>
}

const actions = {
  post: { label: 'Ghi sổ', prompt: 'Ghi sổ chứng từ này và cập nhật tồn kho?' },
  cancel: { label: 'Hủy nháp', prompt: 'Hủy chứng từ nháp này?' },
  reverse: { label: 'Đảo chứng từ', prompt: 'Tạo chứng từ đảo và hoàn tác các biến động của phiếu này?' },
} as const

export function StockDocumentPage({ profile }: { profile: Profile }) {
  const { id } = useParams()
  const navigate = useNavigate()
  const query = useQuery({
    queryKey: ['inventory', 'document', id],
    queryFn: ({ signal }) => api<StockDetail>(root + '/documents/' + id, { signal }),
  })
  const mutation = useMutation({
    mutationFn: (action: keyof typeof actions) => api<StockDetail>(root + '/documents/' + id + '/' + action, { method: 'POST' }),
    onSuccess: result => {
      void queryClient.invalidateQueries({ queryKey: ['inventory'] })
      queryClient.setQueryData(['inventory', 'document', String(result.document.id)], result)
      if (String(result.document.id) !== id) void navigate(docPath + '/' + result.document.id)
    },
  })
  if (query.isPending) return <Loading />
  if (query.isError) return <ErrorNotice error={query.error} />
  const { document, lines, note } = query.data
  const available: (keyof typeof actions)[] = document.status === 1 ? ['cancel', 'post'] :
    document.status === 2 && document.kind !== 6 ? ['reverse'] : []

  return <>
    <Link className="back-link" to={docPath}><ArrowLeft size={16} aria-hidden /> Chứng từ kho</Link>
    <header className="page-heading">
      <div><h1>{document.number}</h1><div className="flex items-center gap-2 mt-2">{kindLabels[document.kind]} {statusBadge(document.status)}</div></div>
      <div className="action-row"><Button variant="outline" onClick={() => window.print()}><Printer size={16} /> In chứng từ</Button>{profile.permissions.includes('Inventory.Post') && <div className="form-actions">
        {available.map(action => <Button
          key={action} variant={action === 'post' ? 'primary' : 'outline'} disabled={mutation.isPending} onClick={() => {
            if (window.confirm(actions[action].prompt)) mutation.mutate(action)
          }}>{actions[action].label}</Button>)}
      </div>}</div>
    </header>
    <ErrorNotice error={mutation.error} />
    <section className="panel record-panel document-summary">
      <h2>Thông tin chứng từ</h2>
      <dl className="key-values">
        <div><dt>Kho</dt><dd>{document.warehouseName}</dd></div>
        {document.destinationName && <div><dt>Kho đích</dt><dd>{document.destinationName}</dd></div>}
        <div><dt>Ngày lập</dt><dd>{time.format(new Date(document.createdAt))}</dd></div>
        {document.postedAt && <div><dt>Ngày ghi sổ</dt><dd>{time.format(new Date(document.postedAt))}</dd></div>}
        {note && <div><dt>Ghi chú</dt><dd>{note}</dd></div>}
      </dl>
      {document.reversesId && <Link className="record-link mt-3 inline-block" to={docPath + '/' + document.reversesId}>
        Mở chứng từ gốc
      </Link>}
    </section>
    <div className="table-panel table-scroll" role="region" aria-label="Dòng chứng từ" tabIndex={0}>
      <table aria-label="Dòng chứng từ">
        <thead><tr><th scope="col">SKU</th><th scope="col">Sản phẩm</th><th scope="col">Số lượng</th><th scope="col">Đơn giá nhập</th></tr></thead>
        <tbody>{lines.map(line => <tr key={line.productId}>
          <td>{line.sku}</td><td>{line.productName}</td><td>{formatQuantity(line.quantity)}</td>
          <td>{line.unitCost === null ? '—' : formatMoney(line.unitCost)}</td>
        </tr>)}</tbody>
      </table>
    </div>
  </>
}

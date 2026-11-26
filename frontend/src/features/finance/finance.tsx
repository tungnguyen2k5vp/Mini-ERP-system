import { useMutation, useQuery } from '@tanstack/react-query'
import { Printer } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router'
import { DataTable } from '../../components/data-table'
import { EmptyState, ErrorNotice, Loading } from '../../components/feedback'
import { Button } from '../../components/ui/button'
import { api, json, type Page, type Profile } from '../../lib/api'
import { queryClient } from '../../lib/query'
import { date, money } from '../../lib/format'

type Invoice = {
  id: number
  stockDocumentId: number
  orderId: number
  orderNumber: string
  partnerId: string
  partnerName: string
  direction: number
  isCredit: boolean
  amount: number
  settled: number
  offsetAmount: number
  outstanding: number
  actionableAmount: number
  issuedAtUtc: string
  dueAtUtc: string
}
type Payment = {
  id: number
  partnerId: string
  direction: number
  isRefund: boolean
  amount: number
  reference: string | null
  postedAtUtc: string
  allocations: { invoiceId: number; amount: number }[]
}
type Aging = {
  partnerId: string
  partnerName: string
  current: number
  days1To30: number
  days31To60: number
  days61To90: number
  daysOver90: number
  balance: number
}
type Reconciliation = {
  direction: number
  invoiced: number
  credits: number
  paid: number
  refunded: number
  balance: number
  outstanding: number
  difference: number
}

export function FinancePage({ profile }: { profile: Profile }) {
  const [params, setParams] = useSearchParams()
  const [tab, setTab] = useState('invoices')
  const [direction, setDirection] = useState(params.get('direction') === '2' ? 2 : 1)
  const [partnerId, setPartner] = useState(params.get('partnerId') ?? '')
  const [invoiceId, setInvoiceId] = useState(params.get('invoiceId') ?? '')
  const [page, setPage] = useState(1)
  const [allocationPage, setAllocationPage] = useState(1)
  const [asOf, setAsOf] = useState(() => new Date().toLocaleDateString('en-CA'))
  const [pay, setPay] = useState(false)
  const [refund, setRefund] = useState(false)
  const [allocations, setAllocations] = useState<Record<string, string>>({})
  const [requestId, setRequestId] = useState(() => crypto.randomUUID())
  const [notice, setNotice] = useState('')
  const options = useQuery({
    queryKey: ['finance', 'options', direction],
    queryFn: ({ signal }) =>
      api<{ partners: { id: string; name: string }[]; methods: { id: string; name: string }[] }>(
        '/finance/options?direction=' + direction,
        { signal },
      ),
  })
  const partners = { ...options, data: options.data?.partners }
  const methods = { ...options, data: options.data?.methods }
  const partnerFilter = partnerId ? '&partnerId=' + encodeURIComponent(partnerId) : ''
  const invoiceFilter = invoiceId ? '&invoiceId=' + encodeURIComponent(invoiceId) : ''
  const invoices = useQuery({
    queryKey: ['finance', 'invoices', direction, partnerId, invoiceId, page],
    queryFn: ({ signal }) =>
      api<Page<Invoice>>(
        `/finance/invoices?direction=${direction}${partnerFilter}${invoiceFilter}&page=${page}&pageSize=20`,
        { signal },
      ),
    enabled: tab === 'invoices',
  })
  const candidates = useQuery({
    queryKey: ['finance', 'allocation-options', direction, partnerId, allocationPage],
    queryFn: ({ signal }) =>
      api<Page<Invoice>>(
        `/finance/invoices?direction=${direction}${partnerFilter}&page=${allocationPage}&pageSize=100`,
        { signal },
      ),
    enabled: pay && Boolean(partnerId),
  })
  const payments = useQuery({
    queryKey: ['finance', 'payments', direction, partnerId, page],
    queryFn: ({ signal }) =>
      api<Page<Payment>>(
        `/finance/payments?direction=${direction}${partnerFilter}&page=${page}&pageSize=20`,
        { signal },
      ),
    enabled: tab === 'payments',
  })
  const aging = useQuery({
    queryKey: ['finance', 'aging', direction, asOf],
    queryFn: ({ signal }) =>
      api<Aging[]>(`/finance/aging?direction=${direction}&asOf=${asOf}T23:59:59Z`, { signal }),
    enabled: tab === 'aging',
  })
  const reconciliation = useQuery({
    queryKey: ['finance', 'reconciliation'],
    queryFn: ({ signal }) => api<Reconciliation[]>('/finance/reconciliation', { signal }),
    enabled: tab === 'reconciliation',
  })
  const save = useMutation({
    mutationFn: (value: object) => api('/finance/payments', json('POST', value)),
    onSuccess: () => {
      setNotice('Đã ghi sổ và phân bổ thanh toán.')
      setPay(false)
      setAllocations({})
      setRequestId(crypto.randomUUID())
      void queryClient.invalidateQueries()
    },
  })
  function reset() {
    setPage(1)
    setAllocationPage(1)
    setPay(false)
    setAllocations({})
    setRequestId(crypto.randomUUID())
    setNotice('')
  }
  const total =
    Object.values(allocations).reduce((sum, n) => sum + Math.round((Number(n) || 0) * 100), 0) / 100
  function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const f = new FormData(e.currentTarget)
    if (!window.confirm(`Ghi sổ ${money(total)} VND và phân bổ cho các hóa đơn đã chọn?`)) return
    save.mutate({
      requestId,
      partnerId,
      direction,
      isRefund: refund,
      paymentMethodId: f.get('method'),
      amount: total,
      reference: f.get('reference'),
      allocations: Object.entries(allocations)
        .filter(([, n]) => Number(n) > 0)
        .map(([invoiceId, amount]) => ({ invoiceId: Number(invoiceId), amount: Number(amount) })),
    })
  }
  const activeQuery =
    tab === 'invoices'
      ? invoices
      : tab === 'payments'
        ? payments
        : tab === 'aging'
          ? aging
          : reconciliation
  return (
    <>
      <header className="page-heading">
        <div>
          <h1>Công nợ & thanh toán</h1>
          <p className="page-description">
            Hóa đơn nội bộ từ chứng từ kho. Phân bổ thu chi và đối soát số dư.
          </p>
        </div>
        <Button variant="outline" onClick={() => window.print()}>
          <Printer size={16} /> In báo cáo
        </Button>
      </header>
      <div className="tabs" role="tablist" aria-label="Tài chính">
        {(
          [
            ['invoices', 'Hóa đơn'],
            ['payments', 'Thu · chi'],
            ['aging', 'Tuổi nợ'],
            ['reconciliation', 'Đối soát'],
          ] as const
        ).map(([key, label]) => (
          <button
            type="button"
            role="tab"
            id={'tab-' + key}
            aria-controls="finance-content"
            aria-selected={tab === key}
            disabled={save.isPending}
            key={key}
            onClick={() => {
              setTab(key)
              reset()
            }}
            onKeyDown={(e) => {
              const keys = ['invoices', 'payments', 'aging', 'reconciliation']
              const index = keys.indexOf(tab)
              const next =
                e.key === 'ArrowRight'
                  ? (index + 1) % 4
                  : e.key === 'ArrowLeft'
                    ? (index + 3) % 4
                    : e.key === 'Home'
                      ? 0
                      : e.key === 'End'
                        ? 3
                        : -1
              if (next >= 0) {
                e.preventDefault()
                setTab(keys[next]!)
                reset()
                document.getElementById('tab-' + keys[next])?.focus()
              }
            }}
            tabIndex={tab === key ? 0 : -1}
          >
            {label}
          </button>
        ))}
      </div>
      <div className="table-toolbar">
        <label className="filter-select">
          <span className="sr-only">Loại công nợ</span>
          <select
            disabled={save.isPending}
            value={direction}
            onChange={(e) => {
              setDirection(Number(e.target.value))
              setPartner('')
              setInvoiceId('')
              reset()
            }}
          >
            <option value="1">Phải thu · khách hàng</option>
            <option value="2">Phải trả · nhà cung cấp</option>
          </select>
        </label>
        {tab !== 'reconciliation' && (
          <label className="filter-select">
            <span className="sr-only">Lọc đối tác</span>
            <select
              aria-label="Lọc đối tác"
              disabled={save.isPending}
              value={partnerId}
              onChange={(e) => {
                setPartner(e.target.value)
                setInvoiceId('')
                reset()
              }}
            >
              <option value="">Tất cả đối tác</option>
              {partners.data?.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
          </label>
        )}
        {tab === 'aging' && (
          <div className="field inline-field">
            <label htmlFor="aging-date">Tính đến</label>
            <input
              id="aging-date"
              type="date"
              required
              value={asOf}
              onChange={(e) => {
                if (e.target.value) setAsOf(e.target.value)
              }}
            />
          </div>
        )}
        {(tab === 'invoices' || tab === 'payments') &&
          profile.permissions.includes('Finance.Post') && (
            <Button
              disabled={save.isPending}
              onClick={() => {
                setPay(!pay)
                setAllocations({})
                setRequestId(crypto.randomUUID())
              }}
            >
              {pay ? 'Đóng lập phiếu' : 'Lập phiếu thanh toán'}
            </Button>
          )}
      </div>
      {invoiceId && tab === 'invoices' && (
        <div className="selected-filter">
          <span>Đang xem hóa đơn #{invoiceId}</span>
          <Button
            variant="outline"
            onClick={() => {
              setInvoiceId('')
              setPage(1)
              const next = new URLSearchParams(params)
              next.delete('invoiceId')
              setParams(next, { replace: true })
            }}
          >
            Xem tất cả hóa đơn
          </Button>
        </div>
      )}
      {notice && (
        <p className="notice notice-good" role="status">
          {notice}
        </p>
      )}
      <ErrorNotice error={activeQuery.error ?? partners.error ?? methods.error} />
      {pay && (
        <section className="panel editor-panel">
          <h2>Lập phiếu {refund ? 'hoàn tiền' : direction === 1 ? 'thu tiền' : 'chi tiền'}</h2>
          {!partnerId ? (
            <p className="notice">Chọn một đối tác ở bộ lọc để phân bổ thanh toán.</p>
          ) : (
            <form onSubmit={submit}>
              <fieldset className="form-stack" disabled={save.isPending}>
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={refund}
                    onChange={(e) => {
                      setRefund(e.target.checked)
                      setAllocationPage(1)
                      setAllocations({})
                      setRequestId(crypto.randomUUID())
                    }}
                  />{' '}
                  Hoàn tiền cho hóa đơn trả hàng
                </label>
                <div className="form-grid">
                  <div className="field">
                    <label htmlFor="payment-method">Phương thức</label>
                    <select
                      id="payment-method"
                      name="method"
                      required
                      onChange={() => setRequestId(crypto.randomUUID())}
                    >
                      <option value="">Chọn phương thức</option>
                      {methods.data?.map((m) => (
                        <option key={m.id} value={m.id}>
                          {m.name}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="field">
                    <label htmlFor="payment-reference">Mã tham chiếu</label>
                    <input
                      id="payment-reference"
                      name="reference"
                      maxLength={100}
                      onChange={() => setRequestId(crypto.randomUUID())}
                    />
                  </div>
                </div>
                <h3>Phân bổ theo hóa đơn</h3>
                <ErrorNotice error={candidates.error} />
                {candidates.isPending ? (
                  <Loading />
                ) : (
                  candidates.data?.items
                    .filter(
                      (i) => i.isCredit === refund && (i.actionableAmount ?? i.outstanding) > 0,
                    )
                    .map((i) => (
                      <div className="allocation-row" key={i.id}>
                        <label htmlFor={'allocation-' + i.id}>
                          <strong>
                            #{i.id} · {i.orderNumber}
                          </strong>
                          <span>
                            Có thể phân bổ {money(i.actionableAmount ?? i.outstanding)} VND · Bù trừ{' '}
                            {money(i.offsetAmount ?? 0)} · {date(i.issuedAtUtc)}
                          </span>
                        </label>
                        <div className="field">
                          <input
                            aria-label={'Phân bổ hóa đơn ' + i.id}
                            id={'allocation-' + i.id}
                            type="number"
                            min="0"
                            max={i.actionableAmount ?? i.outstanding}
                            step="0.01"
                            placeholder="0"
                            value={allocations[i.id] ?? ''}
                            onChange={(e) => {
                              setAllocations({ ...allocations, [i.id]: e.target.value })
                              setRequestId(crypto.randomUUID())
                            }}
                          />
                        </div>
                      </div>
                    ))
                )}
                {candidates.data &&
                  !candidates.data.items.some(
                    (i) => i.isCredit === refund && (i.actionableAmount ?? i.outstanding) > 0,
                  ) && <EmptyState>Không có hóa đơn còn số dư cho loại phiếu này.</EmptyState>}
                {candidates.data && candidates.data.total > 100 && (
                  <div className="pagination">
                    <span>
                      Trang hóa đơn {allocationPage} / {Math.ceil(candidates.data.total / 100)} ·
                      giữ các khoản đã chọn khi chuyển trang
                    </span>
                    <div className="action-row">
                      <Button
                        type="button"
                        variant="outline"
                        disabled={allocationPage <= 1}
                        onClick={() => setAllocationPage(allocationPage - 1)}
                      >
                        Hóa đơn trước
                      </Button>
                      <Button
                        type="button"
                        variant="outline"
                        disabled={allocationPage * 100 >= candidates.data.total}
                        onClick={() => setAllocationPage(allocationPage + 1)}
                      >
                        Hóa đơn tiếp
                      </Button>
                    </div>
                  </div>
                )}
                <div className="document-total">
                  Tổng thanh toán <strong>{money(total)} VND</strong>
                </div>
                <ErrorNotice error={save.error} />
                {Object.values(allocations).filter((n) => Number(n) > 0).length > 100 && (
                  <p className="field-error" role="alert">
                    Mỗi phiếu được phân bổ tối đa 100 hóa đơn.
                  </p>
                )}
                {total > 999_999_999_999 && (
                  <p className="field-error" role="alert">
                    Tổng thanh toán vượt giới hạn 999.999.999.999 VND.
                  </p>
                )}
                <div className="form-actions">
                  <Button
                    disabled={
                      save.isPending ||
                      total <= 0 ||
                      total > 999_999_999_999 ||
                      Object.values(allocations).filter((n) => Number(n) > 0).length > 100
                    }
                  >
                    {save.isPending ? 'Đang ghi sổ…' : 'Ghi sổ thanh toán'}
                  </Button>
                </div>
              </fieldset>
            </form>
          )}
        </section>
      )}
      <div id="finance-content" role="tabpanel" aria-labelledby={'tab-' + tab}>
        <p className="print-only">
          {tab === 'invoices'
            ? 'Hóa đơn nội bộ'
            : tab === 'payments'
              ? 'Phiếu thu chi'
              : tab === 'aging'
                ? 'Tuổi nợ'
                : 'Đối soát'}
          {' · '}
          {direction === 1 ? 'Phải thu khách hàng' : 'Phải trả nhà cung cấp'}
          {' · '}
          {partners.data?.find((partner) => partner.id === partnerId)?.name ?? 'Tất cả đối tác'}
          {tab === 'aging' && ' · Tính đến ' + asOf}
        </p>
        {activeQuery.isPending ? (
          <Loading />
        ) : (
          <>
            {tab === 'invoices' && invoices.data && (
              <DataTable
                label="Hóa đơn nội bộ"
                page={invoices.data}
                onPage={setPage}
                hiddenColumns
                columns={[
                  {
                    accessorKey: 'id',
                    header: 'Hóa đơn',
                    cell: ({ row }) => (
                      <div className="table-identity">
                        <strong>#{row.original.id}</strong>
                        <span>
                          {row.original.isCredit ? 'Điều chỉnh trả hàng' : 'Hóa đơn nội bộ'}
                        </span>
                      </div>
                    ),
                  },
                  {
                    accessorKey: 'orderNumber',
                    header: 'Đơn nguồn',
                    cell: ({ row }) =>
                      profile.permissions.includes(
                        direction === 1 ? 'Sales.View' : 'Purchasing.View',
                      ) ? (
                        <Link
                          className="record-link"
                          to={
                            (direction === 1 ? '/ban-hang/' : '/mua-hang/') + row.original.orderId
                          }
                        >
                          {row.original.orderNumber}
                        </Link>
                      ) : (
                        row.original.orderNumber
                      ),
                  },
                  {
                    accessorKey: 'partnerName',
                    header: 'Đối tác',
                    cell: ({ row }) =>
                      profile.permissions.includes('Catalog.View') ? (
                        <Link
                          className="record-link"
                          to={
                            (direction === 1 ? '/khach-hang/' : '/nha-cung-cap/') +
                            row.original.partnerId
                          }
                        >
                          {row.original.partnerName}
                        </Link>
                      ) : (
                        row.original.partnerName
                      ),
                  },
                  {
                    accessorKey: 'amount',
                    header: 'Giá trị',
                    cell: ({ row }) => money(row.original.amount),
                  },
                  {
                    accessorKey: 'settled',
                    header: 'Đã thanh toán',
                    cell: ({ row }) => money(row.original.settled),
                  },
                  {
                    accessorKey: 'offsetAmount',
                    header: 'Bù trừ trả hàng',
                    cell: ({ row }) => money(row.original.offsetAmount ?? 0),
                  },
                  {
                    accessorKey: 'outstanding',
                    header: 'Còn lại',
                    cell: ({ row }) => (
                      <strong className="number">{money(row.original.outstanding)}</strong>
                    ),
                  },
                  {
                    accessorKey: 'dueAtUtc',
                    header: 'Hạn thanh toán',
                    cell: ({ row }) => date(row.original.dueAtUtc),
                  },
                  {
                    id: 'source',
                    header: 'Chứng từ',
                    cell: ({ row }) => (
                      <Link
                        className="record-link"
                        to={'/ton-kho/chung-tu/' + row.original.stockDocumentId}
                      >
                        Phiếu kho
                      </Link>
                    ),
                  },
                ]}
              />
            )}
            {tab === 'payments' && payments.data && (
              <DataTable
                label="Phiếu thu chi"
                page={payments.data}
                onPage={setPage}
                hiddenColumns
                columns={[
                  { accessorKey: 'id', header: 'Phiếu', cell: ({ row }) => '#' + row.original.id },
                  {
                    accessorKey: 'isRefund',
                    header: 'Loại',
                    cell: ({ row }) =>
                      row.original.isRefund
                        ? 'Hoàn tiền'
                        : direction === 1
                          ? 'Thu tiền'
                          : 'Chi tiền',
                  },
                  {
                    accessorKey: 'partnerId',
                    header: 'Đối tác',
                    cell: ({ row }) =>
                      partners.data?.find((p) => p.id === row.original.partnerId)?.name ??
                      row.original.partnerId,
                  },
                  {
                    accessorKey: 'amount',
                    header: 'Số tiền (VND)',
                    cell: ({ row }) => money(row.original.amount),
                  },
                  { accessorKey: 'reference', header: 'Tham chiếu' },
                  {
                    accessorKey: 'postedAtUtc',
                    header: 'Ngày ghi sổ',
                    cell: ({ row }) => date(row.original.postedAtUtc),
                  },
                  {
                    id: 'allocations',
                    header: 'Phân bổ',
                    cell: ({ row }) =>
                      row.original.allocations
                        .map((a) => `#${a.invoiceId}: ${money(a.amount)}`)
                        .join(' · '),
                  },
                ]}
              />
            )}
            {tab === 'aging' && aging.data && (
              <DataTable
                label="Tuổi nợ theo đối tác"
                page={{
                  items: aging.data.filter((a) => !partnerId || a.partnerId === partnerId),
                  total: aging.data.filter((a) => !partnerId || a.partnerId === partnerId).length,
                  pageNumber: 1,
                  pageSize: Math.max(1, aging.data.length),
                }}
                onPage={() => {}}
                hiddenColumns
                columns={[
                  { accessorKey: 'partnerName', header: 'Đối tác' },
                  ...(
                    [
                      'current',
                      'days1To30',
                      'days31To60',
                      'days61To90',
                      'daysOver90',
                      'balance',
                    ] as const
                  ).map((key, i) => ({
                    accessorKey: key,
                    header: [
                      'Trong hạn',
                      '1–30 ngày',
                      '31–60 ngày',
                      '61–90 ngày',
                      'Trên 90 ngày',
                      'Tổng dư',
                    ][i],
                    cell: ({ row }: { row: { original: Aging } }) => money(row.original[key]),
                  })),
                ]}
              />
            )}
            {tab === 'reconciliation' && reconciliation.data && (
              <>
                <p className="page-description reconciliation-note">
                  Số dư hóa đơn được đối chiếu với các khoản thu chi đã phân bổ. Chênh lệch bằng 0
                  khi sổ khớp.
                </p>
                <DataTable
                  label="Đối soát tài chính"
                  page={{
                    items: reconciliation.data.filter((r) => r.direction === direction),
                    total: 1,
                    pageNumber: 1,
                    pageSize: 1,
                  }}
                  onPage={() => {}}
                  columns={[
                    ...(
                      [
                        'invoiced',
                        'credits',
                        'paid',
                        'refunded',
                        'balance',
                        'outstanding',
                        'difference',
                      ] as const
                    ).map((key, i) => ({
                      accessorKey: key,
                      header: [
                        'Hóa đơn',
                        'Trả hàng',
                        'Thanh toán',
                        'Hoàn tiền',
                        'Số dư sổ',
                        'Công nợ mở',
                        'Chênh lệch',
                      ][i],
                      cell: ({ row }: { row: { original: Reconciliation } }) => (
                        <span
                          className={
                            key === 'difference'
                              ? 'badge ' + (row.original[key] === 0 ? 'badge-good' : 'badge-warn')
                              : 'number'
                          }
                        >
                          {money(row.original[key])}
                        </span>
                      ),
                    })),
                  ]}
                />
              </>
            )}
          </>
        )}
      </div>
    </>
  )
}

import { useQuery } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { DataTable } from '../../components/data-table'
import { ErrorNotice, Loading } from '../../components/feedback'
import { Button } from '../../components/ui/button'
import { api, type Page, type Profile } from '../../lib/api'
import { calendarDate, date, money } from '../../lib/format'
import { CreditExposurePanel, useCreditExposure } from './credit-exposure'

type StatementRow = {
  kind: string; sourceId: number; orderId: number | null; orderNumber: string | null
  invoiceId: number | null; paymentId: number | null; atUtc: string; dueAtUtc: string | null
  amount: number; outstanding: number | null; isOverdue: boolean
}
type Statement = Page<StatementRow> & {
  partnerName: string; direction: number; closingBalance: number; overdue: number
  cutoff: { businessDate: string; inclusive: boolean; atUtc: string }
}
const labels: Record<string, string> = {
  Invoice: 'Hóa đơn', Credit: 'Ghi giảm công nợ', Payment: 'Thanh toán', Refund: 'Hoàn tiền',
  PaymentReversal: 'Đảo thanh toán', RefundReversal: 'Đảo hoàn tiền',
}

export function PartnerStatement({ partnerId, asOf, profile, onDirection }: {
  partnerId: string; asOf: string; profile: Profile; onDirection: (direction: number) => void
}) {
  const [page, setPage] = useState(1)
  const query = useQuery({
    queryKey: ['finance', 'statement', partnerId, asOf, page],
    queryFn: ({ signal }) => api<Statement>(`/finance/statement?partnerId=${encodeURIComponent(partnerId)}&asOfDate=${asOf}&page=${page}&pageSize=20`, { signal }),
    enabled: Boolean(partnerId),
  })
  const actualDirection = query.data?.direction
  useEffect(() => {
    if (actualDirection !== undefined) onDirection(actualDirection)
  }, [actualDirection, onDirection])
  const credit = useCreditExposure('/finance/credit-exposure/' + partnerId, Boolean(partnerId) && actualDirection === 1)
  if (!partnerId) return <p className="notice">Chọn đối tác để xem lịch sử công nợ.</p>
  return <>
    <ErrorNotice error={query.error} />
    {query.isPending ? <Loading /> : query.isError ?
      <Button variant="outline" onClick={() => void query.refetch()}>Tải lại lịch sử công nợ</Button> : query.data && <>
      <section className="panel section-gap">
        <h2>{query.data.partnerName}</h2>
        <p className="page-description">{query.data.cutoff.inclusive
          ? 'Tính đến ' + date(query.data.cutoff.atUtc) + ' (giờ Việt Nam, bao gồm thời điểm chốt).'
          : 'Tính đến hết ngày ' + calendarDate(query.data.cutoff.businessDate) + ' theo giờ Việt Nam.'} Số tiền dương tăng công nợ; số tiền âm giảm công nợ.</p>
        <dl className="key-values">
          <div><dt>Số dư cuối kỳ</dt><dd>{money(query.data.closingBalance)} VND</dd></div>
          <div><dt>Quá hạn</dt><dd>{money(query.data.overdue)} VND</dd></div>
        </dl>
      </section>
      <DataTable label="Lịch sử công nợ đối tác" page={query.data} onPage={setPage} columns={[
        { accessorKey: 'atUtc', header: 'Ngày ghi nhận', cell: ({ row }) => date(row.original.atUtc) },
        { accessorKey: 'kind', header: 'Nghiệp vụ', cell: ({ row }) => labels[row.original.kind] ?? row.original.kind },
        { accessorKey: 'sourceId', header: 'Nguồn', cell: ({ row }) => <span>#{row.original.sourceId}</span> },
        { accessorKey: 'orderNumber', header: 'Đơn nguồn', cell: ({ row }) => row.original.orderId &&
          profile.permissions.includes(actualDirection === 1 ? 'Sales.View' : 'Purchasing.View') ?
          <Link className="record-link" to={(actualDirection === 1 ? '/ban-hang/' : '/mua-hang/') + row.original.orderId}>{row.original.orderNumber}</Link> : row.original.orderNumber ?? '—' },
        { accessorKey: 'amount', header: 'Tăng / giảm nợ (VND)', cell: ({ row }) => money(row.original.amount) },
        { accessorKey: 'outstanding', header: 'Dư hóa đơn (VND)', cell: ({ row }) => money(row.original.outstanding) },
        { accessorKey: 'dueAtUtc', header: 'Hạn thanh toán', cell: ({ row }) => <>{row.original.dueAtUtc ? date(row.original.dueAtUtc) : '—'} {row.original.isOverdue && <span className="badge badge-warn">Quá hạn</span>}</> },
      ]} />
    </>}
    {actualDirection === 1 && <CreditExposurePanel query={credit} />}
  </>
}

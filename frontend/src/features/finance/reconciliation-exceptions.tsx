import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { DataTable } from '../../components/data-table'
import { ErrorNotice, Loading } from '../../components/feedback'
import { Button } from '../../components/ui/button'
import { api, type Page, type Profile } from '../../lib/api'
import { calendarDate, date, money } from '../../lib/format'

type ExceptionRow = {
  code: string
  entity: string
  entityId: string
  orderId?: number | null
  stockDocumentId?: number | null
  invoiceId?: number | null
  paymentId?: number | null
  sourceOrderKind?: number | null
  invoiceDirection?: number | null
  expected?: number | null
  actual?: number | null
  difference?: number | null
  expectedReference?: string | null
  actualReference?: string | null
  message: string
}
type ExceptionPage = Page<ExceptionRow> & {
  cutoff: { atUtc: string; inclusive: boolean; businessDate: string | null }
}

export function ReconciliationExceptions({
  asOfDate,
  direction,
  profile,
}: {
  asOfDate: string
  direction: number
  profile: Profile
}) {
  const [page, setPage] = useState(1)
  const parameters = new URLSearchParams({
    asOfDate,
    direction: String(direction),
    page: String(page),
    pageSize: '15',
  })
  const query = useQuery({
    queryKey: ['finance', 'exceptions', parameters.toString()],
    queryFn: ({ signal }) =>
      api<ExceptionPage>('/finance/reconciliation/exceptions?' + parameters, { signal }),
  })
  return (
    <section className="section-gap" aria-labelledby="finance-exceptions-heading">
      <h2 id="finance-exceptions-heading">Ngoại lệ đối soát theo chứng từ</h2>
      {query.data?.cutoff && (
        <p className="page-description reconciliation-note">
          {query.data.cutoff.businessDate
            ? 'Chốt hết ngày ' +
              calendarDate(query.data.cutoff.businessDate) +
              ' theo giờ Việt Nam.'
            : (query.data.cutoff.inclusive ? 'Chốt đến ' : 'Chốt trước ') +
              date(query.data.cutoff.atUtc) +
              ' (giờ Việt Nam).'}
        </p>
      )}
      <ErrorNotice error={query.error} />
      <div className="table-toolbar">
        <Button variant="outline" disabled={query.isFetching} onClick={() => void query.refetch()}>
          Kiểm tra lại chứng từ
        </Button>
      </div>
      {query.isPending ? (
        <Loading label="Đang kiểm tra chứng từ tài chính" />
      ) : (
        !query.error &&
        query.data &&
        (query.data.total === 0 ? (
          <p className="notice notice-good" role="status">
            Không phát hiện ngoại lệ tài chính trong phạm vi và ngày chốt đã chọn.
          </p>
        ) : (
          <>
            <p className="notice notice-error" role="status">
              Có {query.data.total.toLocaleString('vi-VN')} ngoại lệ cần kiểm tra riêng; các chênh
              lệch trái dấu không bù trừ nhau trong danh sách này.
            </p>
            <DataTable
              label="Ngoại lệ đối soát tài chính"
              page={query.data}
              onPage={setPage}
              hiddenColumns
              columns={[
                {
                  accessorKey: 'code',
                  header: 'Ngoại lệ',
                  enableHiding: false,
                  cell: ({ row }) => (
                    <div className="table-identity">
                      <strong>{row.original.code}</strong>
                      <span>{row.original.message}</span>
                    </div>
                  ),
                },
                {
                  accessorKey: 'entityId',
                  header: 'Đối tượng · mã',
                  cell: ({ row }) => (
                    <div className="table-identity">
                      {row.original.entity}
                      <span>{row.original.entityId}</span>
                    </div>
                  ),
                },
                ...(['expected', 'actual', 'difference'] as const).map((key, i) => ({
                  accessorKey: key,
                  header: ['Kỳ vọng', 'Thực tế', 'Chênh lệch'][i],
                  cell: ({ row }: { row: { original: ExceptionRow } }) => (
                    <span className="number">{money(row.original[key] ?? null)}</span>
                  ),
                })),
                {
                  id: 'references',
                  header: 'Tham chiếu kỳ vọng · thực tế',
                  cell: ({ row }) => (
                    <div className="table-identity">
                      <span>{row.original.expectedReference ?? '—'}</span>
                      <span>{row.original.actualReference ?? '—'}</span>
                    </div>
                  ),
                },
                {
                  id: 'sources',
                  header: 'Chứng từ nguồn',
                  cell: ({ row }) => (
                    <div className="exception-sources">
                      {row.original.orderId != null &&
                        ((row.original.sourceOrderKind === 1 || row.original.sourceOrderKind === 2) &&
                        profile.permissions.includes(
                          row.original.sourceOrderKind === 2 ? 'Sales.View' : 'Purchasing.View',
                        ) ? (
                          <Link
                            className="record-link"
                            to={
                              (row.original.sourceOrderKind === 2 ? '/ban-hang/' : '/mua-hang/') + row.original.orderId
                            }
                          >
                            Đơn #{row.original.orderId}
                          </Link>
                        ) : (
                          <span>Đơn #{row.original.orderId}</span>
                        ))}
                      {row.original.stockDocumentId != null &&
                        (profile.permissions.includes('Inventory.View') ? (
                          <Link
                            className="record-link"
                            to={'/ton-kho/chung-tu/' + row.original.stockDocumentId}
                          >
                            Kho #{row.original.stockDocumentId}
                          </Link>
                        ) : (
                          <span>Kho #{row.original.stockDocumentId}</span>
                        ))}
                      {row.original.invoiceId != null &&
                        (profile.permissions.includes('Finance.View') &&
                        (row.original.invoiceDirection === 1 || row.original.invoiceDirection === 2) ? (
                        <a
                          className="record-link"
                          href={
                            '/tai-chinh?' +
                            new URLSearchParams({
                              direction: String(row.original.invoiceDirection),
                              invoiceId: String(row.original.invoiceId),
                            })
                          }
                        >
                          Hóa đơn #{row.original.invoiceId}
                        </a>
                        ) : (
                          <span>Hóa đơn #{row.original.invoiceId}</span>
                        ))}
                      {row.original.paymentId != null && (
                        <span>Thanh toán #{row.original.paymentId}</span>
                      )}
                    </div>
                  ),
                },
              ]}
            />
          </>
        ))
      )}
    </section>
  )
}

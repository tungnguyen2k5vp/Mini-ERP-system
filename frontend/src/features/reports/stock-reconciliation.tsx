import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { DataTable } from '../../components/data-table'
import { ErrorNotice, Loading } from '../../components/feedback'
import { Button } from '../../components/ui/button'
import { api, type Page } from '../../lib/api'
import { money } from '../../lib/format'

type StockException = {
  warehouseId: string
  warehouse: string
  productId: string
  sku: string
  onHand: number
  ledgerQuantity: number
  reserved: number
  orderReservations: number
  openCommitment: number
  inconsistentOrderLines: number
  hasBalance: boolean
}

export function StockReconciliation({ warehouseId }: { warehouseId: string }) {
  const [page, setPage] = useState(1)
  const parameters = new URLSearchParams({ page: String(page), pageSize: '20' })
  if (warehouseId) parameters.set('warehouseId', warehouseId)
  const query = useQuery({
    queryKey: ['stock-reconciliation', parameters.toString()],
    queryFn: ({ signal }) =>
      api<Page<StockException>>('/reports/stock/reconciliation?' + parameters, { signal }),
  })
  return (
    <section aria-labelledby="stock-reconciliation-heading">
      <h2 id="stock-reconciliation-heading">Đối soát tồn kho hiện tại</h2>
      <ErrorNotice error={query.error} />
      <div className="table-toolbar">
        <Button variant="outline" disabled={query.isFetching} onClick={() => void query.refetch()}>
          Kiểm tra lại tồn kho
        </Button>
      </div>
      {query.isPending ? (
        <Loading label="Đang đối soát tồn kho" />
      ) : (
        !query.error &&
        query.data &&
        (query.data.total === 0 ? (
          <p className="notice notice-good" role="status">
            Không phát hiện sai lệch tồn kho trong phạm vi đã chọn.
          </p>
        ) : (
          <>
            <p className="notice notice-error" role="status">
              Phát hiện {query.data.total.toLocaleString('vi-VN')} vị trí cần kiểm tra. Mở lịch sử
              SKU để truy vết trước khi điều chỉnh.
            </p>
            <DataTable
              label="Sai lệch tồn kho hiện tại"
              page={query.data}
              onPage={setPage}
              hiddenColumns
              columns={[
                {
                  accessorKey: 'sku',
                  header: 'SKU · lịch sử',
                  enableHiding: false,
                  cell: ({ row }) => (
                    <div className="table-identity">
                      <Link
                        className="record-link"
                        to={
                          '/ton-kho/lich-su?' +
                          new URLSearchParams({
                            productId: row.original.productId,
                            warehouseId: row.original.warehouseId,
                          })
                        }
                      >
                        {row.original.sku}
                      </Link>
                    </div>
                  ),
                },
                { accessorKey: 'warehouse', header: 'Kho' },
                ...(
                  [
                    'onHand',
                    'ledgerQuantity',
                    'reserved',
                    'orderReservations',
                    'openCommitment',
                    'inconsistentOrderLines',
                  ] as const
                ).map((key, i) => ({
                  accessorKey: key,
                  header: [
                    'Số dư thực tế',
                    'Tổng sổ kho',
                    'Số dư đang giữ',
                    'Giữ theo đơn',
                    'Cam kết chưa giao',
                    'Dòng đơn lệch',
                  ][i],
                  cell: ({ row }: { row: { original: StockException } }) => (
                    <span className="number">{money(row.original[key])}</span>
                  ),
                })),
                {
                  accessorKey: 'hasBalance',
                  header: 'Bản ghi số dư',
                  cell: ({ row }) => (row.original.hasBalance ? 'Có' : 'Thiếu bản ghi'),
                },
              ]}
            />
          </>
        ))
      )}
    </section>
  )
}

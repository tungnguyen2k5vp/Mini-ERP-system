import { useQuery } from '@tanstack/react-query'
import { ErrorNotice, Loading } from '../../components/feedback'
import { Button } from '../../components/ui/button'
import { api } from '../../lib/api'
import { money } from '../../lib/format'

export type CreditExposure = {
  creditLimit: number; outstanding: number; unbilled: number; used: number
  available: number | null; orderAmount?: number; projected?: number; requiresOverride?: boolean
}

export function useCreditExposure(path: string, enabled: boolean) {
  return useQuery({
    queryKey: ['credit-exposure', path],
    queryFn: ({ signal }) => api<CreditExposure>(path, { signal }),
    enabled,
    staleTime: 0,
  })
}

export function CreditExposurePanel({ query, showProjection = false }: { query: ReturnType<typeof useCreditExposure>; showProjection?: boolean }) {
  return <section className="panel section-gap" aria-label="Hạn mức công nợ hiện tại">
    <h2>Hạn mức công nợ hiện tại</h2>
    <p className="page-description">Số liệu hiện tại, không theo ngày chốt báo cáo. Đơn đã duyệt chưa xuất hóa đơn cũng sử dụng hạn mức.</p>
    <ErrorNotice error={query.error} />
    {query.isPending ? <Loading /> : query.isError ?
      <Button variant="outline" onClick={() => void query.refetch()}>Tải lại hạn mức</Button> : query.data &&
      <dl className="key-values">
        <div><dt>Hạn mức</dt><dd>{query.data.available === null ? 'Không giới hạn' : money(query.data.creditLimit) + ' VND'}</dd></div>
        <div><dt>Nợ hóa đơn</dt><dd>{money(query.data.outstanding)} VND</dd></div>
        <div><dt>Chưa xuất hóa đơn</dt><dd>{money(query.data.unbilled)} VND</dd></div>
        <div><dt>Đã sử dụng</dt><dd>{money(query.data.used)} VND</dd></div>
        <div><dt>Còn khả dụng</dt><dd>{query.data.available === null ? 'Không giới hạn' : money(query.data.available) + ' VND'}</dd></div>
        {showProjection && query.data.orderAmount !== undefined && query.data.orderAmount > 0 && <>
          <div><dt>Giá trị đơn này</dt><dd>{money(query.data.orderAmount)} VND</dd></div>
          {query.data.projected !== undefined && <div><dt>Sau duyệt đơn này</dt><dd>{money(query.data.projected)} VND</dd></div>}
        </>}
      </dl>}
  </section>
}

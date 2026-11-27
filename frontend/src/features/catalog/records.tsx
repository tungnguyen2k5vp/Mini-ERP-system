import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery } from '@tanstack/react-query'
import type { ColumnDef } from '@tanstack/react-table'
import { ArrowLeft, Pencil } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useBeforeUnload, useNavigate, useParams } from 'react-router'
import { z } from 'zod'
import { ErrorNotice, Loading } from '../../components/feedback'
import { Button } from '../../components/ui/button'
import { api, json, type Profile } from '../../lib/api'
import { formatMoney } from '../../lib/labels'
import { queryClient } from '../../lib/query'
import { ActiveBadge, Listing, type CatalogRecord } from './listing'
import { records, type Resource } from './routes'
type RecordRow = CatalogRecord & { code: string; name: string; email?: string | null; phone?: string | null;
  address?: string | null; taxCode?: string | null; creditLimit?: number; paymentTermsDays?: number }

const schema = z.object({
  code: z.string().trim().regex(/^[A-Za-z0-9_.-]{2,32}$/, 'Dùng từ 2 đến 32 ký tự chữ, số, dấu gạch hoặc dấu chấm.'),
  name: z.string().trim().min(1, 'Nhập tên.').max(160),
  isActive: z.boolean(),
  phone: z.string().trim().max(32),
  email: z.union([z.literal(''), z.email('Nhập địa chỉ email hợp lệ.').max(254)]),
  address: z.string().trim().max(250),
  taxCode: z.string().trim().max(32),
  creditLimit: z.number('Nhập hạn mức hợp lệ.').min(0).max(999_999_999_999),
  paymentTermsDays: z.number('Nhập số ngày hợp lệ.').int().min(0).max(365),
})

const isPartner = (resource: Resource) => resource === 'customers' || resource === 'suppliers'

export function RecordsPage({ profile, resource }: { profile: Profile; resource: Resource }) {
  const config = records[resource]
  const columns: ColumnDef<RecordRow>[] = [
    { accessorKey: 'code', header: 'Mã', enableHiding: false, cell: ({ row }) => <span className="record-code">{row.original.code}</span> },
    { accessorKey: 'name', header: 'Tên', enableHiding: false, cell: ({ row }) =>
      <Link className="record-link" to={config.path + '/' + row.original.id}>{row.original.name}</Link> },
    ...(isPartner(resource) ? [
      { accessorKey: 'email', header: 'Email', enableSorting: false },
      { accessorKey: 'paymentTermsDays', header: 'Kỳ hạn (ngày)', enableSorting: false },
    ] : []),
    { id: 'status', header: 'Trạng thái', cell: ({ row }) => <ActiveBadge active={row.original.isActive} /> },
  ]
  return <Listing profile={profile} resource={resource} path={config.path} title={config.title}
    create={'Thêm ' + config.singular} columns={columns} />
}

function useRecord(resource: Resource, id: string | undefined) {
  return useQuery({
    queryKey: ['catalog', resource, id],
    queryFn: ({ signal }) => api<RecordRow>('/' + resource + '/' + id, { signal }),
    enabled: Boolean(id),
  })
}

export function RecordDetail({ profile, resource }: { profile: Profile; resource: Resource }) {
  const { id } = useParams()
  const query = useRecord(resource, id)
  const config = records[resource]
  if (query.isPending) return <Loading />
  if (query.isError) return <ErrorNotice error={query.error} />
  const row = query.data
  const fields = [['Mã', row.code]]
  if (isPartner(resource)) {
    fields.push(['Email', row.email ?? '—'], ['Điện thoại', row.phone ?? '—'], ['Địa chỉ', row.address ?? '—'],
      ['Mã số thuế', row.taxCode ?? '—'], ['Kỳ hạn thanh toán', String(row.paymentTermsDays ?? 0) + ' ngày'])
    if (resource === 'customers') fields.push(['Hạn mức công nợ', row.creditLimit === 0 ? 'Không giới hạn' : formatMoney(row.creditLimit ?? 0)])
  }
  return <>
    <Link className="back-link" to={config.path}><ArrowLeft size={16} aria-hidden /> {config.title}</Link>
    <header className="page-heading">
      <div><h1>{row.name}</h1><div className="mt-2"><ActiveBadge active={row.isActive} /></div></div>
      {profile.permissions.includes('Catalog.Update') && <Button asChild><Link to={config.path + '/' + row.id + '/sua'}>
        <Pencil size={16} aria-hidden /> Chỉnh sửa
      </Link></Button>}
    </header>
    <section className="panel record-panel">
      <h2>Thông tin {config.singular}</h2>
      <dl className="key-values">{fields.map(([label, value]) =>
        <div key={label}><dt>{label}</dt><dd>{value}</dd></div>
      )}</dl>
      {isPartner(resource) && <div className="action-row section-gap">
        {profile.permissions.includes(resource === 'customers' ? 'Sales.View' : 'Purchasing.View') && <Button variant="outline" asChild><Link to={(resource === 'customers' ? '/ban-hang' : '/mua-hang') + '?partnerId=' + row.id}>Lịch sử đơn hàng</Link></Button>}
        {profile.permissions.includes('Finance.View') && <Button variant="outline" asChild><Link to={'/tai-chinh?direction=' + (resource === 'customers' ? '1' : '2') + '&partnerId=' + row.id}>Công nợ & giao dịch</Link></Button>}
      </div>}
    </section>
  </>
}

export function RecordForm({ resource, profile }: { resource: Resource; profile: Profile }) {
  const { id } = useParams()
  const query = useRecord(resource, id)
  if (id && query.isPending) return <Loading />
  if (query.isError) return <ErrorNotice error={query.error} />
  return <RecordEditor key={resource + (query.data?.id ?? '')} resource={resource} row={query.data} profile={profile} />
}

function RecordEditor({ resource, row, profile }: { resource: Resource; row?: RecordRow; profile: Profile }) {
  const config = records[resource]
  const navigate = useNavigate()
  const [version] = useState(row?.version)
  const limitReadOnly = resource === 'customers' && Boolean(row) && !profile.permissions.includes('Finance.ChangeCreditLimit')
  const form = useForm<z.infer<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: {
      code: row?.code ?? '', name: row?.name ?? '', isActive: row?.isActive ?? true,
      phone: row?.phone ?? '', email: row?.email ?? '', address: row?.address ?? '',
      taxCode: row?.taxCode ?? '', creditLimit: row?.creditLimit ?? 0, paymentTermsDays: row?.paymentTermsDays ?? 0,
    },
  })
  const { errors, isDirty } = form.formState
  const save = useMutation({
    mutationFn: (value: z.infer<typeof schema>) => {
      const basic = { code: value.code, name: value.name, isActive: value.isActive, version }
      const input = isPartner(resource) ? { ...value, ...basic, email: value.email || null,
        creditLimit: limitReadOnly ? row!.creditLimit : value.creditLimit } : basic
      return api<RecordRow>('/' + resource + (row ? '/' + row.id : ''), json(row ? 'PUT' : 'POST', input))
    },
    onSuccess: result => {
      form.reset()
      void queryClient.invalidateQueries({ queryKey: ['catalog', resource] })
      queryClient.setQueryData(['catalog', resource, result.id], result)
      void navigate(config.path + '/' + result.id, { replace: true })
    },
  })
  useBeforeUnload(event => {
    if (isDirty && !save.isSuccess) { event.preventDefault(); event.returnValue = true }
  })
  const basicFields = [{ name: 'code', label: 'Mã' }, { name: 'name', label: 'Tên' }] as const
  const contacts = [{ name: 'email', label: 'Email' }, { name: 'phone', label: 'Điện thoại' },
    { name: 'address', label: 'Địa chỉ' }, { name: 'taxCode', label: 'Mã số thuế' },
    { name: 'paymentTermsDays', label: 'Kỳ hạn thanh toán (ngày)' }] as const
  const fields = isPartner(resource) ? [...basicFields, ...contacts,
    ...(resource === 'customers' ? [{ name: 'creditLimit', label: 'Hạn mức công nợ (VND)' } as const] : [])] : basicFields

  return <>
    <header className="page-heading"><h1>{row ? 'Chỉnh sửa ' : 'Thêm '}{config.singular}</h1></header>
    <form className="panel record-panel form-stack" noValidate onSubmit={form.handleSubmit(value => save.mutate(value))}>
      <div className="form-grid">{fields.map(field => {
        const numeric = field.name === 'paymentTermsDays' || field.name === 'creditLimit'
        return <div className="field" key={field.name}>
          <label htmlFor={field.name}>{field.label}</label>
          <input id={field.name} type={numeric ? 'number' : field.name === 'email' ? 'email' : 'text'}
            readOnly={field.name === 'creditLimit' && limitReadOnly}
            aria-invalid={Boolean(errors[field.name])} aria-describedby={errors[field.name] ? field.name + '-error' : undefined}
            min={numeric ? 0 : undefined} step={field.name === 'creditLimit' ? '0.0001' : undefined}
            {...form.register(field.name, { valueAsNumber: numeric })} />
          {errors[field.name] && <span className="field-error" id={field.name + '-error'}>{errors[field.name]?.message}</span>}
          {field.name === 'creditLimit' && limitReadOnly && <small>Chỉ đọc. Cần quyền thay đổi hạn mức công nợ để điều chỉnh.</small>}
        </div>
      })}</div>
      <label className="checkbox-row"><input type="checkbox" {...form.register('isActive')} /> Đang sử dụng</label>
      <ErrorNotice error={save.error} />
      <div className="form-actions">
        <Button type="button" variant="outline" disabled={save.isPending} onClick={() => {
          if (!isDirty || window.confirm('Bỏ thay đổi chưa lưu?')) void navigate(config.path + (row ? '/' + row.id : ''))
        }}>Hủy</Button>
        <Button type="submit" disabled={save.isPending}>{save.isPending ? 'Đang lưu…' : 'Lưu ' + config.singular}</Button>
      </div>
    </form>
  </>
}

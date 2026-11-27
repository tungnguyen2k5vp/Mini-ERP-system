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
import { formatMoney, formatQuantity } from '../../lib/labels'
import { queryClient } from '../../lib/query'
import { ActiveBadge, Listing, useLookupOptions, type CatalogRecord } from './listing'
import { ProductWorkbook } from './workbook'

const path = '/san-pham'
const prices = [
  { name: 'purchasePrice', label: 'Giá mua (VND)' },
  { name: 'salePrice', label: 'Giá bán (VND)' },
  { name: 'minimumStock', label: 'Tồn tối thiểu' },
] as const

type Product = CatalogRecord & {
  sku: string; name: string; categoryId: string; categoryName: string; unitId: string; unitName: string
  purchasePrice: number | null; salePrice: number; minimumStock: number; description: string | null; imageUrl: string | null
  createdAt: string; updatedAt: string
}

const amount = z.number('Nhập một số hợp lệ.').min(0, 'Giá trị cần lớn hơn hoặc bằng 0.').max(999_999_999_999)
const productInput = z.object({
  sku: z.string().trim().regex(/^[A-Za-z0-9_.-]{2,32}$/, 'Dùng từ 2 đến 32 ký tự chữ, số, dấu gạch hoặc dấu chấm.'),
  name: z.string().trim().min(1, 'Nhập tên sản phẩm.').max(160),
  categoryId: z.uuid('Chọn nhóm hàng.'),
  unitId: z.uuid('Chọn đơn vị tính.'),
  purchasePrice: amount,
  salePrice: amount,
  minimumStock: amount,
  description: z.string().max(500),
  imageUrl: z.union([z.literal(''), z.url({ protocol: /^https$/ }).max(500)]),
  isActive: z.boolean(),
})

const columns: ColumnDef<Product>[] = [
  { accessorKey: 'sku', header: 'SKU', cell: ({ row }) => <span className="record-code">{row.original.sku}</span>, enableHiding: false },
  { accessorKey: 'name', header: 'Tên sản phẩm', cell: ({ row }) =>
    <Link className="record-link" to={path + '/' + row.original.id}>{row.original.name}</Link>, enableHiding: false },
  { accessorKey: 'categoryName', header: 'Nhóm hàng', enableSorting: false },
  { accessorKey: 'unitName', header: 'ĐVT', enableSorting: false },
  { accessorKey: 'salePrice', header: 'Giá bán', cell: ({ row }) => <span className="number">{formatMoney(row.original.salePrice)}</span> },
  { accessorKey: 'minimumStock', header: 'Tồn tối thiểu', cell: ({ row }) => formatQuantity(row.original.minimumStock) },
  { id: 'status', header: 'Trạng thái', cell: ({ row }) => <ActiveBadge active={row.original.isActive} /> },
]

export function ProductsPage({ profile }: { profile: Profile }) {
  const categories = useLookupOptions('categories')
  return <Listing profile={profile} resource="products" path={path} title="Sản phẩm"
    description="Quản lý SKU, giá và quy cách hàng hóa." create="Thêm sản phẩm" columns={columns} categories={categories.data}
    tools={<ProductWorkbook profile={profile} />} />
}

function useProduct(id?: string) {
  return useQuery({
    queryKey: ['catalog', 'products', id],
    queryFn: ({ signal }) => api<Product>('/products/' + id, { signal }),
    enabled: Boolean(id),
  })
}

export function ProductDetail({ profile }: { profile: Profile }) {
  const { id } = useParams()
  const query = useProduct(id)
  if (query.isPending) return <Loading />
  if (query.isError) return <ErrorNotice error={query.error} />
  const product = query.data
  const fields = [
    ['SKU', product.sku], ['Nhóm hàng', product.categoryName], ['Đơn vị tính', product.unitName],
    ['Giá mua', formatMoney(product.purchasePrice)], ['Giá bán', formatMoney(product.salePrice)],
    ['Tồn tối thiểu', formatQuantity(product.minimumStock)],
    ['Cập nhật lần cuối', new Date(product.updatedAt).toLocaleString('vi-VN')],
  ]
  return <>
    <Link className="back-link" to={path}><ArrowLeft size={16} aria-hidden /> Sản phẩm</Link>
    <header className="page-heading">
      <div><h1>{product.name}</h1><div className="mt-2"><ActiveBadge active={product.isActive} /></div></div>
      {profile.permissions.includes('Catalog.Update') && <Button asChild><Link to={path + '/' + product.id + '/sua'}>
        <Pencil size={16} aria-hidden /> Chỉnh sửa
      </Link></Button>}
    </header>
    <section className="panel record-panel">
      <h2>Thông tin sản phẩm</h2>
      <dl className="key-values">
        {fields.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}
      </dl>
      {product.description && <p className="muted mt-4">{product.description}</p>}
      {product.imageUrl && <a className="record-link mt-4 inline-block" href={product.imageUrl}
        target="_blank" rel="noreferrer">Mở ảnh sản phẩm</a>}
    </section>
  </>
}

export function ProductForm() {
  const { id } = useParams()
  const query = useProduct(id)
  if (id && query.isPending) return <Loading />
  if (query.isError) return <ErrorNotice error={query.error} />
  if (id && query.data?.purchasePrice === null) return <p role="alert">Chưa tải được giá để chỉnh sửa. Hãy tải lại trang.</p>
  return <ProductEditor key={query.data?.id ?? 'new'} product={query.data} />
}

function ProductEditor({ product }: { product?: Product }) {
  const navigate = useNavigate()
  const [version] = useState(product?.version)
  const category = useLookupOptions('categories', product?.categoryId)
  const units = useLookupOptions('units', product?.unitId)
  const form = useForm<z.infer<typeof productInput>>({
    resolver: zodResolver(productInput),
    defaultValues: {
      sku: product?.sku ?? '', name: product?.name ?? '', categoryId: product?.categoryId ?? '',
      unitId: product?.unitId ?? '', purchasePrice: product?.purchasePrice ?? 0, salePrice: product?.salePrice ?? 0,
      minimumStock: product?.minimumStock ?? 0, description: product?.description ?? '',
      imageUrl: product?.imageUrl ?? '', isActive: product?.isActive ?? true,
    },
  })
  const { errors, isDirty } = form.formState
  const save = useMutation({
    mutationFn: (value: z.infer<typeof productInput>) =>
      api<Product>(product ? '/products/' + product.id : '/products',
        json(product ? 'PUT' : 'POST', { ...value, imageUrl: value.imageUrl || null, version })),
    onSuccess: result => {
      form.reset()
      void queryClient.invalidateQueries({ queryKey: ['catalog', 'products'] })
      queryClient.setQueryData(['catalog', 'products', result.id], result)
      void navigate(path + '/' + result.id, { replace: true })
    },
  })

  useBeforeUnload(event => {
    if (isDirty && !save.isSuccess) { event.preventDefault(); event.returnValue = true }
  })
  const cancel = () => {
    if (!isDirty || window.confirm('Bỏ thay đổi chưa lưu?')) void navigate(product ? path + '/' + product.id : path)
  }

  return <>
    <header className="page-heading">
      <div><h1>{product ? 'Chỉnh sửa sản phẩm' : 'Thêm sản phẩm'}</h1>
        {product && <p className="page-description">{product.sku} · {product.name}</p>}</div>
    </header>
    {category.isPending || units.isPending ? <Loading /> : <form noValidate className="panel form-stack record-panel"
      onSubmit={form.handleSubmit(value => save.mutate(value))}>
      <ErrorNotice error={category.error ?? units.error} />
      <div className="form-grid">
        {([
          { name: 'sku', label: 'SKU' }, { name: 'name', label: 'Tên sản phẩm' },
          ...prices, { name: 'imageUrl', label: 'Đường dẫn ảnh (tùy chọn)' },
        ] as const).map(field => {
          const numeric = prices.some(item => item.name === field.name)
          return <div className="field" key={field.name}>
            <label htmlFor={field.name}>{field.label}</label>
            <input id={field.name} type={numeric ? 'number' : field.name === 'imageUrl' ? 'url' : 'text'}
              step={numeric ? '0.0001' : undefined} min={numeric ? 0 : undefined} aria-invalid={Boolean(errors[field.name])}
              aria-describedby={errors[field.name] ? field.name + '-error' : undefined}
              {...form.register(field.name, { valueAsNumber: numeric })} />
            {errors[field.name] && <span className="field-error" id={field.name + '-error'}>{errors[field.name]?.message}</span>}
          </div>
        })}
        {([{ name: 'categoryId', label: 'Nhóm hàng', data: category.data },
          { name: 'unitId', label: 'Đơn vị tính', data: units.data }] as const).map(field =>
          <div className="field" key={field.name}>
            <label htmlFor={field.name}>{field.label}</label>
            <select id={field.name} aria-invalid={Boolean(errors[field.name])}
              aria-describedby={errors[field.name] ? field.name + '-error' : undefined} {...form.register(field.name)}>
              <option value="">Chọn {field.label.toLowerCase()}</option>
              {field.data?.map(item => <option key={item.id} value={item.id}>
                {item.name}{item.isActive ? '' : ' · Ngừng sử dụng'}
              </option>)}
            </select>
            {errors[field.name] && <span className="field-error" id={field.name + '-error'}>{errors[field.name]?.message}</span>}
          </div>)}
      </div>
      <div className="field"><label htmlFor="description">Mô tả (tùy chọn)</label>
        <textarea id="description" rows={3} maxLength={500} {...form.register('description')} />
      </div>
      <label className="checkbox-row"><input type="checkbox" {...form.register('isActive')} /> Đang sử dụng</label>
      <ErrorNotice error={save.error} />
      <div className="form-actions">
        <Button type="button" variant="outline" onClick={cancel} disabled={save.isPending}>Hủy</Button>
        <Button type="submit" disabled={save.isPending}>{save.isPending ? 'Đang lưu…' : 'Lưu sản phẩm'}</Button>
      </div>
    </form>}
  </>
}

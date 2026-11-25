import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useFieldArray, useForm, useWatch } from 'react-hook-form'
import { useBeforeUnload, useNavigate } from 'react-router'
import { z } from 'zod'
import { ErrorNotice, Loading } from '../../components/feedback'
import { Button } from '../../components/ui/button'
import { api, json } from '../../lib/api'
import { queryClient } from '../../lib/query'
import { useLookupOptions } from '../catalog/listing'
import type { StockDetail } from './inventory'

const schema = (kind: number) => z.object({
  warehouseId: z.uuid('Chọn kho.'),
  destinationId: z.string(),
  note: z.string().max(300),
  lines: z.array(z.object({
    productId: z.uuid('Chọn sản phẩm.'),
    quantity: z.number('Nhập số lượng.').min(-999_999_999_999).max(999_999_999_999),
    unitCost: z.number('Nhập giá vốn.').min(0).max(999_999_999_999).optional(),
  })).min(1).max(50),
}).superRefine((value, ctx) => {
  if (kind === 3 && (!value.destinationId || value.destinationId === value.warehouseId)) {
    ctx.addIssue({ code: 'custom', path: ['destinationId'], message: 'Chọn kho đích khác kho nguồn.' })
  }
  if ((kind === 4 || kind === 5) && !value.note.trim()) {
    ctx.addIssue({ code: 'custom', path: ['note'], message: 'Nhập lý do điều chỉnh hoặc kiểm kê.' })
  }
  value.lines.forEach((line, index) => {
    if ((kind === 4 && line.quantity === 0) || (kind === 5 && line.quantity < 0) ||
        (kind <= 3 && line.quantity <= 0)) {
      ctx.addIssue({ code: 'custom', path: ['lines', index, 'quantity'], message: 'Số lượng chưa phù hợp với loại phiếu.' })
    }
    if (kind === 1 && line.unitCost === undefined) {
      ctx.addIssue({ code: 'custom', path: ['lines', index, 'unitCost'], message: 'Nhập giá vốn hàng nhập.' })
    }
    if (value.lines.findIndex(previous => previous.productId === line.productId) !== index) {
      ctx.addIssue({ code: 'custom', path: ['lines', index, 'productId'], message: 'Sản phẩm đã có trên phiếu.' })
    }
  })
})

type StockProduct = { id: string; sku: string; name: string; purchasePrice: number }
type StockInput = z.infer<ReturnType<typeof schema>>
const blankLine = { productId: '', quantity: 1, unitCost: undefined }

export function StockForm() {
  const [requestId] = useState(() => crypto.randomUUID())
  const [kind, setKind] = useState(1)
  const navigate = useNavigate()
  const warehouses = useLookupOptions('warehouses')
  const products = useQuery({
    queryKey: ['inventory', 'product-options'],
    queryFn: ({ signal }) => api<StockProduct[]>('/inventory/product-options', { signal }),
  })
  const form = useForm<StockInput>({
    resolver: zodResolver(schema(kind)),
    defaultValues: { warehouseId: '', destinationId: '', note: '', lines: [blankLine] },
  })
  const warehouseId = useWatch({ control: form.control, name: 'warehouseId' })
  const lines = useFieldArray({ control: form.control, name: 'lines' })
  const { errors, isDirty } = form.formState
  const dirty = isDirty || kind !== 1
  const save = useMutation({
    mutationFn: (value: StockInput) => api<StockDetail>('/inventory/documents', json('POST', {
      ...value, requestId, kind, destinationId: kind === 3 ? value.destinationId : null,
      lines: value.lines.map(line => ({ ...line, unitCost: kind === 2 || kind === 3 ? null : line.unitCost ?? null })),
    })),
    onSuccess: result => {
      form.reset()
      void queryClient.invalidateQueries({ queryKey: ['inventory', 'documents'] })
      queryClient.setQueryData(['inventory', 'document', String(result.document.id)], result)
      void navigate('/ton-kho/chung-tu/' + result.document.id, { replace: true })
    },
  })
  useBeforeUnload(event => {
    if (dirty && !save.isSuccess) { event.preventDefault(); event.returnValue = true }
  })

  const costEditable = kind === 1 || kind === 4 || kind === 5
  return <>
    <header className="page-heading"><div><h1>Lập phiếu kho</h1>
      <p className="page-description">Phiếu nháp chỉ tác động đến tồn kho sau khi ghi sổ.</p></div></header>
    <ErrorNotice error={warehouses.error ?? products.error} />
    {warehouses.isPending || products.isPending ? <Loading /> : <form className="panel form-stack"
      noValidate onSubmit={form.handleSubmit(value => save.mutate(value))}>
      <div className="form-grid">
        <div className="field">
          <label htmlFor="stock-kind">Loại phiếu</label>
          <select id="stock-kind" value={kind} onChange={event => setKind(Number(event.target.value))}>
            <option value="1">Nhập kho</option><option value="2">Xuất kho</option><option value="3">Chuyển kho</option>
            <option value="4">Điều chỉnh</option><option value="5">Kiểm kê</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor="stock-warehouse">{kind === 3 ? 'Kho nguồn' : 'Kho'}</label>
          <select id="stock-warehouse" aria-invalid={Boolean(errors.warehouseId)} {...form.register('warehouseId')}>
            <option value="">Chọn kho</option>
            {warehouses.data?.map(row => <option key={row.id} value={row.id}>{row.name}</option>)}
          </select>
          {errors.warehouseId && <p className="field-error" role="alert">{errors.warehouseId.message}</p>}
        </div>
        {kind === 3 && <div className="field">
          <label htmlFor="stock-destination">Kho đích</label>
          <select id="stock-destination" aria-invalid={Boolean(errors.destinationId)} {...form.register('destinationId')}>
            <option value="">Chọn kho đích</option>
            {warehouses.data?.filter(row => row.id !== warehouseId).map(row => <option key={row.id} value={row.id}>{row.name}</option>)}
          </select>
          {errors.destinationId && <p className="field-error" role="alert">{errors.destinationId.message}</p>}
        </div>}
        <div className="field"><label htmlFor="stock-note">Ghi chú</label>
          <input id="stock-note" maxLength={300} aria-invalid={Boolean(errors.note)} {...form.register('note')} />
          {errors.note && <p className="field-error" role="alert">{errors.note.message}</p>}
        </div>
      </div>
      <fieldset className="stock-lines">
        <legend>Hàng hóa</legend>
        {lines.fields.map((line, index) => {
          const error = errors.lines?.[index]
          return <div key={line.id} className="stock-line">
            <div className="field stock-line-product">
              <label htmlFor={'product-' + line.id}>Sản phẩm {index + 1}</label>
              <select id={'product-' + line.id} aria-invalid={Boolean(error?.productId)}
                {...form.register(`lines.${index}.productId`, { onChange: event => {
                  const selected = products.data?.find(row => row.id === event.target.value)
                  if (selected && kind === 1) form.setValue(`lines.${index}.unitCost`, selected.purchasePrice)
                } })}>
                <option value="">Chọn sản phẩm</option>
                {products.data?.map(row => <option key={row.id} value={row.id}>{row.sku} · {row.name}</option>)}
              </select>
              {error?.productId && <p className="field-error" role="alert">{error.productId.message}</p>}
            </div>
            <div className="field">
              <label htmlFor={'quantity-' + line.id}>{kind === 5 ? 'Số lượng đếm' : 'Số lượng'}</label>
              <input id={'quantity-' + line.id} type="number" step="0.0001" aria-invalid={Boolean(error?.quantity)}
                {...form.register(`lines.${index}.quantity`, { valueAsNumber: true })} />
              {error?.quantity && <p className="field-error" role="alert">{error.quantity.message}</p>}
            </div>
            {costEditable && <div className="field">
              <label htmlFor={'cost-' + line.id}>Giá vốn (VND)</label>
              <input id={'cost-' + line.id} type="number" min="0" step="0.0001" aria-invalid={Boolean(error?.unitCost)}
                {...form.register(`lines.${index}.unitCost`, { setValueAs: value => value === '' ? undefined : Number(value) })} />
              {error?.unitCost && <p className="field-error" role="alert">{error.unitCost.message}</p>}
            </div>}
            <Button variant="ghost" className="icon-button" type="button" onClick={() => lines.remove(index)}
              aria-label={'Xóa dòng ' + (index + 1)} disabled={lines.fields.length === 1}><Trash2 size={17} /></Button>
          </div>
        })}
      </fieldset>
      <div><Button type="button" variant="outline" onClick={() => lines.append(blankLine)} disabled={lines.fields.length >= 50}>
        <Plus size={16} aria-hidden /> Thêm dòng
      </Button></div>
      <ErrorNotice error={save.error} />
      <div className="form-actions">
        <Button type="button" variant="outline" disabled={save.isPending} onClick={() => {
          if (!dirty || window.confirm('Bỏ thay đổi chưa lưu?')) void navigate('/ton-kho/chung-tu')
        }}>Hủy</Button>
        <Button type="submit" disabled={save.isPending}>{save.isPending ? 'Đang lưu…' : 'Lưu phiếu nháp'}</Button>
      </div>
    </form>}
  </>
}

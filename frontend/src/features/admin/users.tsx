import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery } from '@tanstack/react-query'
import type { ColumnDef } from '@tanstack/react-table'
import { Plus, Search } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { DataTable } from '../../components/data-table'
import { ErrorNotice, Loading } from '../../components/feedback'
import { Button } from '../../components/ui/button'
import { api, json, type Page } from '../../lib/api'
import { roleLabel } from '../../lib/labels'
import { queryClient } from '../../lib/query'
import { useDebouncedValue } from '../../lib/use-debounced-value'
import { email, password } from '../auth/schemas'

type Account = {
  id: string; email: string; displayName: string; role: string
  isDisabled: boolean; isLockedOut: boolean; version: string
}
export type Role = { name: string; permissions: string[]; version: string }

const schema = z.object({
  email,
  displayName: z.string().trim().min(1, 'Nhập họ và tên.').max(100),
  password,
  role: z.string().min(1, 'Chọn vai trò.'),
})

type EditorMode = { account?: Account } | null

export function UsersPage() {
  const [page, setPage] = useState(1)
  const [term, setTerm] = useState('')
  const [editor, setEditor] = useState<EditorMode>(null)
  const [message, setMessage] = useState('')
  const search = useDebouncedValue(term)
  const query = useQuery({
    queryKey: ['users', page, search],
    queryFn: ({ signal }) => api<Page<Account>>('/users?' + new URLSearchParams({ page: String(page), search }), { signal }),
  })

  const roles = useQuery({
    queryKey: ['roles'],
    queryFn: ({ signal }) => api<Role[]>('/roles', { signal }),
  })

  const columns: ColumnDef<Account>[] = [
    { accessorKey: 'displayName', header: 'Họ và tên', cell: ({ row }) =>
      <div className="table-identity"><strong>{row.original.displayName}</strong><span>{row.original.email}</span></div> },
    { accessorKey: 'role', header: 'Vai trò', cell: ({ row }) => roleLabel(row.original.role) },
    { id: 'status', header: 'Trạng thái', cell: ({ row }) => row.original.isDisabled || row.original.isLockedOut
      ? <span className="badge badge-warn">{row.original.isDisabled ? 'Đã khóa' : 'Tạm khóa'}</span>
      : <span className="badge badge-good">Hoạt động</span> },
    { id: 'actions', header: 'Thao tác', cell: ({ row }) => row.original.role === 'Administrator' ? '—' :
      <Button variant="ghost" type="button" onClick={() => { setMessage(''); setEditor({ account: row.original }) }}
        aria-label={'Sửa quyền của ' + row.original.displayName}>Sửa quyền</Button> },
  ]

  function finish() {
    setMessage(editor?.account ? 'Đã cập nhật quyền truy cập.' : 'Đã tạo tài khoản.')
    setEditor(null)
    void queryClient.invalidateQueries({ queryKey: ['users'] })
  }

  return <>
    <header className="page-heading">
      <div><h1>Người dùng</h1><p className="page-description">Tạo tài khoản, gán vai trò và quản lý quyền đăng nhập.</p></div>
      <Button type="button" onClick={() => { setMessage(''); setEditor({}) }}><Plus size={17} aria-hidden /> Tạo tài khoản</Button>
    </header>
    {message && <p className="notice notice-good" role="status">{message}</p>}
    {editor && <section className="panel editor-panel">
      <h2>{editor.account ? 'Cập nhật quyền · ' + editor.account.displayName : 'Tạo tài khoản'}</h2>
      <ErrorNotice error={roles.error} />
      {roles.isPending ? <Loading /> : roles.data &&
        (editor.account ? <AccountEditor key={editor.account.id} account={editor.account} roles={roles.data}
          onDone={finish} onCancel={() => setEditor(null)} /> :
          <AccountCreator roles={roles.data} onDone={finish} onCancel={() => setEditor(null)} />)}
    </section>}
    <div className="table-toolbar">
      <label className="search-box"><Search size={17} aria-hidden /><span className="sr-only">Tìm tài khoản</span>
        <input type="search" maxLength={120} placeholder="Tìm theo tên hoặc email" value={term}
          onChange={event => { setTerm(event.target.value); setPage(1) }} /></label>
    </div>
    <ErrorNotice error={query.error} />
    {query.isPending ? <Loading /> : query.data &&
      <DataTable page={query.data} columns={columns} label="Danh sách người dùng" onPage={setPage} />}
  </>
}

type EditorProps = { roles: Role[]; onDone: () => void; onCancel: () => void }

function AccountCreator({ roles, onDone, onCancel }: EditorProps) {
  const form = useForm<z.infer<typeof schema>>({ resolver: zodResolver(schema), defaultValues: { role: 'Viewer' } })
  const create = useMutation({ mutationFn: (value: z.infer<typeof schema>) => api<Account>('/users', json('POST', value)), onSuccess: onDone })
  const { errors } = form.formState

  return <form noValidate className="form-stack" onSubmit={form.handleSubmit(value => create.mutate(value))}>
    <div className="form-grid">
      {(['displayName', 'email', 'password'] as const).map(name => <div className="field" key={name}>
        <label htmlFor={'create-' + name}>{({ displayName: 'Họ và tên', email: 'Email', password: 'Mật khẩu ban đầu' })[name]}</label>
        <input id={'create-' + name} type={name === 'displayName' ? 'text' : name}
          autoComplete={name === 'password' ? 'new-password' : undefined}
          aria-invalid={Boolean(errors[name])} aria-describedby={errors[name] ? 'error-' + name : undefined}
          {...form.register(name)} />
        {errors[name] && <span className="field-error" id={'error-' + name}>{errors[name]?.message}</span>}
      </div>)}
      <div className="field"><label htmlFor="create-role">Vai trò</label>
        <select id="create-role" {...form.register('role')}>{roles.map(role =>
          <option key={role.name} value={role.name}>{roleLabel(role.name)}</option>)}</select>
      </div>
    </div>
    <ErrorNotice error={create.error} />
    <EditorActions pending={create.isPending} onCancel={onCancel} label="Tạo tài khoản" />
  </form>
}

function AccountEditor({ account, roles, onDone, onCancel }: EditorProps & { account: Account }) {
  const [role, setRole] = useState(account.role)
  const [isDisabled, setDisabled] = useState(account.isDisabled)
  const update = useMutation({
    mutationFn: () => api<Account>('/users/' + account.id, json('PUT', { role, isDisabled, version: account.version })),
    onSuccess: onDone,
  })

  return <form className="form-stack" onSubmit={event => {
    event.preventDefault()
    if (window.confirm('Lưu quyền mới và kết thúc các phiên đăng nhập của tài khoản này?')) update.mutate()
  }}>
    <div className="field"><label htmlFor="edit-role">Vai trò</label>
      <select id="edit-role" value={role} onChange={event => setRole(event.target.value)}>
        {roles.map(item => <option key={item.name} value={item.name}>{roleLabel(item.name)}</option>)}
      </select>
    </div>
    <label className="checkbox-row"><input type="checkbox" checked={isDisabled}
      onChange={event => setDisabled(event.target.checked)} /> Khóa đăng nhập</label>
    {account.isLockedOut && !isDisabled && <p className="muted">Khi lưu, khóa tạm thời sẽ được gỡ.</p>}
    <ErrorNotice error={update.error} />
    <EditorActions pending={update.isPending} onCancel={onCancel} label="Lưu quyền" />
  </form>
}

function EditorActions({ pending, onCancel, label }: { pending: boolean; onCancel: () => void; label: string }) {
  return <div className="form-actions">
    <Button type="button" variant="outline" disabled={pending} onClick={onCancel}>Hủy</Button>
    <Button type="submit" disabled={pending}>{pending ? 'Đang lưu…' : label}</Button>
  </div>
}

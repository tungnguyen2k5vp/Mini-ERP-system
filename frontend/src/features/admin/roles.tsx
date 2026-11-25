import { useMutation, useQuery } from '@tanstack/react-query'
import { LockKeyhole } from 'lucide-react'
import { useState } from 'react'
import { ErrorNotice, Loading } from '../../components/feedback'
import { Button } from '../../components/ui/button'
import { api, json } from '../../lib/api'
import { permissionLabel, roleLabel } from '../../lib/labels'
import { queryClient } from '../../lib/query'
import type { Role } from './users'

export function RolesPage() {
  const [selected, setSelected] = useState('Manager')
  const roles = useQuery({ queryKey: ['roles'], queryFn: ({ signal }) => api<Role[]>('/roles', { signal }) })
  const permissions = useQuery({
    queryKey: ['permissions'], queryFn: ({ signal }) => api<string[]>('/permissions', { signal }),
  })
  const role = roles.data?.find(item => item.name === selected)
  return <>
    <header className="page-heading">
      <div><h1>Vai trò và quyền</h1><p className="page-description">Chọn vai trò rồi điều chỉnh các quyền nghiệp vụ.</p></div>
    </header>
    <ErrorNotice error={roles.error ?? permissions.error} />
    {roles.isPending || permissions.isPending ? <Loading /> : role && permissions.data &&
      <div className="role-grid">
        <div className="panel role-list" aria-label="Các vai trò">
          <h2>Vai trò</h2>
          {roles.data?.map(item => <button key={item.name} type="button" onClick={() => setSelected(item.name)}
            aria-pressed={item.name === selected}>{roleLabel(item.name)}</button>)}
        </div>
        <RoleEditor key={role.name + role.version} role={role} permissions={permissions.data} />
      </div>}
  </>
}

function RoleEditor({ role, permissions }: { role: Role; permissions: string[] }) {
  const [grants, setGrants] = useState(role.permissions)
  const isAdmin = role.name === 'Administrator'
  const update = useMutation({
    mutationFn: () => api<Role>('/roles/' + encodeURIComponent(role.name), json('PUT', { permissions: grants, version: role.version })),
    onSuccess: result => {
      queryClient.setQueryData<Role[]>(['roles'], previous => previous?.map(item => item.name === result.name ? result : item))
    },
  })
  const changed = grants.length !== role.permissions.length || grants.some(value => !role.permissions.includes(value))
  return <section className="panel">
    <h2>{roleLabel(role.name)}</h2>
    {isAdmin && <p className="notice"><LockKeyhole size={17} aria-hidden /> Quyền quản trị được thiết lập sẵn.</p>}
    <form className="form-stack" onSubmit={event => {
      event.preventDefault()
      if (window.confirm('Áp dụng các quyền này cho mọi tài khoản thuộc vai trò?')) update.mutate()
    }}>
      <fieldset disabled={isAdmin || update.isPending} className="permission-fieldset">
        <legend className="sr-only">Quyền nghiệp vụ</legend>
        {permissions.filter(permission => isAdmin || !['System.Users', 'System.Roles'].includes(permission)).map(permission =>
          <label className="checkbox-row" key={permission}>
            <input type="checkbox" checked={grants.includes(permission)} onChange={event =>
              setGrants(event.target.checked ? [...grants, permission] : grants.filter(value => value !== permission))} />
            {permissionLabel(permission)}
          </label>)}
      </fieldset>
      <ErrorNotice error={update.error} />
      {!isAdmin && <div><Button type="submit" disabled={!changed || update.isPending}>Lưu phân quyền</Button></div>}
    </form>
  </section>
}

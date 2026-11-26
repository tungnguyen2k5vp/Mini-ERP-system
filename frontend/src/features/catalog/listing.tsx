import { keepPreviousData, useQuery } from '@tanstack/react-query'
import type { ColumnDef, SortingState } from '@tanstack/react-table'
import { Plus, Search } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Link } from 'react-router'
import { DataTable } from '../../components/data-table'
import { ErrorNotice, Loading } from '../../components/feedback'
import { Button } from '../../components/ui/button'
import { api, type Page, type Profile } from '../../lib/api'
import { useDebouncedValue } from '../../lib/use-debounced-value'

export type CatalogRecord = { id: string; isActive: boolean; version: string }
export type LookupOption = { id: string; name: string; isActive: boolean }

export function useLookupOptions(resource: string, includedId?: string, enabled = true) {
  return useQuery({
    queryKey: ['catalog', resource, 'options', includedId],
    enabled,
    queryFn: ({ signal }) => api<LookupOption[]>(
      '/' + resource + '/options' + (includedId ? '?includedId=' + encodeURIComponent(includedId) : ''), { signal }),
  })
}

export function ActiveBadge({ active }: { active: boolean }) {
  return <span className={'badge ' + (active ? 'badge-good' : 'badge-warn')}>
    {active ? 'Đang sử dụng' : 'Ngừng sử dụng'}
  </span>
}

export function Listing<T extends CatalogRecord>({ profile, resource, path, title, description, create, columns, categories, tools }:
  { profile: Profile; resource: string; path: string; title: string; description?: string;
    create: string; columns: ColumnDef<T>[]; categories?: LookupOption[]; tools?: ReactNode }) {
  const [page, setPage] = useState(1)
  const [text, setText] = useState('')
  const [active, setActive] = useState('')
  const [category, setCategory] = useState('')
  const [sorting, setSorting] = useState<SortingState>([])
  const search = useDebouncedValue(text)
  const sort = sorting[0]
  const parameters = new URLSearchParams({ page: String(page), pageSize: '15', search })
  if (active) parameters.set('active', active)
  if (category) parameters.set('categoryId', category)
  if (sort) {
    parameters.set('sort', sort.id)
    parameters.set('descending', String(sort.desc))
  }
  const query = useQuery({
    queryKey: ['catalog', resource, parameters.toString()],
    queryFn: ({ signal }) => api<Page<T>>('/' + resource + '?' + parameters.toString(), { signal }),
    placeholderData: keepPreviousData,
  })

  return <>
    <header className="page-heading">
      <div><h1>{title}</h1>{description && <p className="page-description">{description}</p>}</div>
      {profile.permissions.includes('Catalog.Create') && <Button asChild><Link to={path + '/moi'}>
        <Plus size={17} aria-hidden /> {create}
      </Link></Button>}
    </header>
    {tools}
    <div className="table-toolbar catalog-toolbar">
      <label className="search-box"><Search size={17} aria-hidden /><span className="sr-only">Tìm {title.toLocaleLowerCase('vi')}</span>
        <input type="search" maxLength={120} value={text} placeholder="Tìm theo mã hoặc tên"
          onChange={event => { setText(event.target.value); setPage(1) }} />
      </label>
      {categories && <label className="filter-select"><span className="sr-only">Lọc nhóm hàng</span>
        <select value={category} onChange={event => { setCategory(event.target.value); setPage(1) }}>
          <option value="">Tất cả nhóm hàng</option>
          {categories.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
        </select>
      </label>}
      <label className="filter-select"><span className="sr-only">Lọc trạng thái</span>
        <select value={active} onChange={event => { setActive(event.target.value); setPage(1) }}>
          <option value="">Tất cả trạng thái</option>
          <option value="true">Đang sử dụng</option>
          <option value="false">Ngừng sử dụng</option>
        </select>
      </label>
    </div>
    <ErrorNotice error={query.error} />
    {query.isPending ? <Loading /> : query.data &&
      <div aria-busy={query.isFetching}>
        <DataTable page={query.data} columns={columns} label={title} onPage={setPage} sorting={sorting}
          onSorting={value => { setSorting(value); setPage(1) }} hiddenColumns />
      </div>}
  </>
}

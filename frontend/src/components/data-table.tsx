import { flexRender, getCoreRowModel, useReactTable, type ColumnDef, type SortingState } from '@tanstack/react-table'
import { ArrowDown, ArrowUp, ArrowUpDown, ChevronLeft, ChevronRight, Columns3 } from 'lucide-react'
import type { Page } from '../lib/api'
import { EmptyState } from './feedback'
import { Button } from './ui/button'

export function DataTable<T>({ page, columns, label, onPage, sorting = [], onSorting, hiddenColumns = false }:
  { page: Page<T>; columns: ColumnDef<T>[]; label: string; onPage: (page: number) => void;
    sorting?: SortingState; onSorting?: (sort: SortingState) => void; hiddenColumns?: boolean }) {
  // TanStack Table retains its own visibility state while the server owns paging and sorting.
  // eslint-disable-next-line react-hooks/incompatible-library
  const table = useReactTable({
    data: page.items, columns, getCoreRowModel: getCoreRowModel(), manualSorting: true, manualPagination: true,
    state: { sorting }, onSortingChange: update => onSorting?.(typeof update === 'function' ? update(sorting) : update),
    enableSorting: Boolean(onSorting), enableMultiSort: false,
  })
  const pageCount = Math.max(1, Math.ceil(page.total / page.pageSize))
  return <div className="table-panel">
    {hiddenColumns && <details className="column-control">
      <summary><Columns3 size={16} aria-hidden /> Cột hiển thị</summary>
      <div>{table.getAllLeafColumns().filter(column => column.getCanHide()).map(column =>
        <label key={column.id}><input type="checkbox" checked={column.getIsVisible()} onChange={column.getToggleVisibilityHandler()} />
          {typeof column.columnDef.header === 'string' ? column.columnDef.header : column.id}</label>)}</div>
    </details>}
    {page.items.length === 0 ? <EmptyState>Không tìm thấy bản ghi phù hợp.</EmptyState> : <>
      <p className="table-mobile-hint">Cuộn ngang để xem thêm cột.</p>
      <div className="table-scroll" role="region" aria-label={label} tabIndex={0}>
        <table aria-label={label}>
          <thead>{table.getHeaderGroups().map(group => <tr key={group.id}>{group.headers.map(header => {
            const sort = header.column.getIsSorted()
            const icon = sort === 'asc' ? <ArrowUp size={13} /> : sort === 'desc' ? <ArrowDown size={13} /> : <ArrowUpDown size={13} />
            const title = flexRender(header.column.columnDef.header, header.getContext())
            return <th key={header.id} scope="col" aria-sort={sort === 'asc' ? 'ascending' : sort === 'desc' ? 'descending' : undefined}>
              {header.column.getCanSort() ? <button className="table-sort" onClick={header.column.getToggleSortingHandler()}>
                {title}<span aria-hidden>{icon}</span></button> : title}
            </th>
          })}</tr>)}</thead>
          <tbody>{table.getRowModel().rows.map(row => <tr key={row.id}>{row.getVisibleCells().map(cell =>
            <td key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</td>)}</tr>)}</tbody>
        </table>
      </div></>}
    <div className="pagination">
      <span>{page.total.toLocaleString('vi-VN')} bản ghi</span>
      <div className="flex items-center gap-2">
        <Button type="button" variant="outline" className="icon-button" disabled={page.pageNumber <= 1}
          onClick={() => onPage(page.pageNumber - 1)} aria-label="Trang trước"><ChevronLeft size={18} /></Button>
        <span>Trang {page.pageNumber} / {pageCount}</span>
        <Button type="button" variant="outline" className="icon-button" disabled={page.pageNumber >= pageCount}
          onClick={() => onPage(page.pageNumber + 1)} aria-label="Trang sau"><ChevronRight size={18} /></Button>
      </div>
    </div>
  </div>
}

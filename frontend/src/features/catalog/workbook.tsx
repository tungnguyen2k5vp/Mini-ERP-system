import { useMutation } from '@tanstack/react-query'
import { Download, Upload } from 'lucide-react'
import { useRef, useState } from 'react'
import { ErrorNotice } from '../../components/feedback'
import { Button } from '../../components/ui/button'
import { api, download, type Profile } from '../../lib/api'
import { queryClient } from '../../lib/query'

export function ProductWorkbook({ profile }: { profile: Profile }) {
  const input = useRef<HTMLInputElement>(null)
  const [notice, setNotice] = useState('')
  const exportFile = useMutation({
    mutationFn: () => download('/catalog/products/workbook', 'MiniErp-san-pham.xlsx'),
  })
  const upload = useMutation({
    mutationFn: (file: File) => {
      const body = new FormData()
      body.append('file', file)
      return api<{ imported: number }>('/catalog/products/workbook', { method: 'POST', body })
    },
    onSuccess: (result) => {
      setNotice(`Đã nhập ${result.imported} sản phẩm.`)
      void queryClient.invalidateQueries({ queryKey: ['catalog'] })
    },
  })
  if (
    !profile.permissions.includes('Catalog.Create') &&
    !profile.permissions.includes('Catalog.Export')
  )
    return null
  return (
    <div className="workbook-tools">
      <div className="action-row">
        {profile.permissions.includes('Catalog.Export') && (
          <Button
            variant="outline"
            disabled={exportFile.isPending}
            onClick={() => exportFile.mutate()}
          >
            <Download size={16} /> Xuất Excel
          </Button>
        )}
        {profile.permissions.includes('Catalog.Create') && (
          <>
            <input
              ref={input}
              type="file"
              accept=".xlsx"
              hidden
              aria-label="Tệp Excel sản phẩm"
              onChange={(e) => {
                const file = e.target.files?.[0]
                if (file) {
                  setNotice('')
                  upload.mutate(file)
                }
                e.target.value = ''
              }}
            />
            <Button
              variant="outline"
              disabled={upload.isPending}
              onClick={() => input.current?.click()}
            >
              <Upload size={16} />
              {upload.isPending ? 'Đang kiểm tra tệp…' : 'Nhập Excel'}
            </Button>
          </>
        )}
      </div>
      <details className="muted report-footnote">
        <summary>Hướng dẫn nhập Excel</summary>
        <p>
          Dùng tệp xuất làm mẫu, giữ nguyên tiêu đề cột và chỉ giữ các dòng cần nhập với SKU mới.
          Dán giá trị, dùng định dạng General và không dùng công thức. Tệp .xlsx tối đa 2 MB; toàn
          bộ tệp được kiểm tra trước khi lưu.
        </p>
      </details>
      <ErrorNotice error={exportFile.error ?? upload.error} />
      {notice && (
        <p className="notice notice-good" role="status">
          {notice}
        </p>
      )}
    </div>
  )
}

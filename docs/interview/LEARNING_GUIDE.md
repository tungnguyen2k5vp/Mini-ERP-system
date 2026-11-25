# Đọc MiniErp trong 7 ngày

| Ngày | Đọc và lần theo luồng | Thực hành |
| --- | --- | --- |
| 1 | Program.cs, các .csproj và sơ đồ thành phần. Theo request từ middleware đến route. | Mở OpenAPI, sửa mô tả một route. Tự trả lời framework chọn endpoint và scope DI ở đâu. |
| 2 | AuthEndpoints.cs, IdentityService.cs, AuthSettings.cs. Theo login đến phiên SQL. | Chạy bài test xoay token, thử thay thời hạn JWT trong môi trường thử. Giải thích SQL lưu giá trị nào của refresh token. |
| 3 | Permissions.cs và PermissionHandler.cs. Theo quyền Viewer đến HTTP 403. | Tạo tài khoản Viewer, thử mở trang người dùng và bổ sung một assertion cho quyền đọc. Giải thích cấp quyền khác với khóa đăng nhập như thế nào. |
| 4 | CatalogService.cs, CatalogModel.cs, catalog/products.tsx. Theo form đến SQL và lỗi 409. | Chỉnh cùng sản phẩm trên hai tab. Thêm một bộ lọc và giải thích chỗ chặn SKU trùng hay cập nhật cũ. |
| 5 | StockRecords.cs, StockService.cs và InventoryTests.cs. Theo nháp đến ghi sổ. | Tính giá vốn khi nhập 100 ở giá 100 và nhập 50 ở giá 160. Thay một lượng xuất trong unit test và dự đoán số dư. |
| 6 | StockLedger migration, InventoryReport.cs và database/performance. Theo báo cáo đến procedure. | Chạy execution plan cho SKU. Thêm bộ lọc SQL theo kho và giải thích tác động của CHARINDEX lên khả năng seek. |
| 7 | inventory/stock-form.tsx, inventory.spec.ts và báo cáo kiểm thử. Theo thao tác đến phiếu đảo. | Thử chuyển và đảo 3 đơn vị, thêm một test cho thao tác hủy nháp. Giải thích vì sao tổng tồn giữ nguyên khi chuyển kho. |

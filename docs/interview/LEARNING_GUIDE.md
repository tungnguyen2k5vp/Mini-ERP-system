# Đọc MiniErp trong 7 ngày

| Ngày | Đọc và lần theo luồng | Thực hành |
| --- | --- | --- |
| 1 | Program.cs, các .csproj và sơ đồ thành phần. Theo request từ middleware đến route. | Mở OpenAPI, sửa mô tả một route. Tự trả lời framework chọn endpoint và scope DI ở đâu. |
| 2 | AuthEndpoints.cs, IdentityService.cs, AuthSettings.cs. Theo login đến phiên SQL. | Chạy bài test xoay token, thử thay thời hạn JWT trong môi trường thử. Giải thích SQL lưu giá trị nào của refresh token. |
| 3 | Permissions.cs và PermissionHandler.cs. Theo quyền Viewer đến HTTP 403. | Tạo tài khoản Viewer, thử mở trang người dùng và bổ sung một assertion cho quyền đọc. Giải thích cấp quyền khác với khóa đăng nhập như thế nào. |
| 4 | CatalogService.cs, CatalogModel.cs, catalog/products.tsx. Theo form đến SQL và lỗi 409. | Chỉnh cùng sản phẩm trên hai tab. Thêm một bộ lọc và giải thích chỗ chặn SKU trùng hay cập nhật cũ. |
| 5 | StockRecords.cs, StockService.cs và InventoryTests.cs. Theo nháp đến ghi sổ. | Tính giá vốn khi nhập 100 ở giá 100 và nhập 50 ở giá 160. Thay một lượng xuất trong unit test và dự đoán số dư. |
| 6 | OrderService.cs, FinanceService.cs, Settlement.cs và các integration test. Lần theo một lần giao một phần từ giữ hàng đến hóa đơn và thu tiền. | Giải thích vì sao hai đơn tranh tồn chỉ có một đơn được duyệt. Tính số còn thu khi giao 100, trả 40 và đã thu 30. Kiểm tra lại bằng truy vấn phân bổ theo hóa đơn. |
| 7 | OperationsReport.cs, reports.tsx, Playwright và GitHub Actions. Lần theo doanh thu và giá vốn từ sổ kho đến dashboard. | Chạy 50 bài SQL trên MiniErp_Test. Chọn một ca Playwright rồi đối chiếu dữ liệu trên dashboard với phiếu nguồn và giải thích kiểm thử chạy ở đâu trong CI. |

[Hướng dẫn demo](../DEMO_GUIDE.md) · [Bộ câu hỏi và bài tập](README.md)

# Giải thích thiết kế MiniErp

- **Vì sao .NET và SQL Server?** ASP.NET Core cung cấp pipeline HTTP và Identity. SQL Server cung cấp transaction, rowversion, stored procedure và execution plan cho nghiệp vụ kho.
- **Vì sao modular monolith?** Tồn kho, trạng thái phiếu và nhật ký cần cùng ranh giới transaction. Một tiến trình đủ cho các module hiện tại.
- **Clean Architecture ở đâu?** StockBalance và StockDocument ở Domain. DTO và quyền ở Application. EF, Identity và Dapper ở Infrastructure. API sở hữu HTTP và cấu hình.
- **Vì sao EF Core lẫn Dapper?** StockService theo dõi và lưu đồ thị thay đổi. InventoryReport gọi procedure tham số hóa và nhận hai tập kết quả bằng Dapper.
- **Làm sao chặn xuất vượt tồn?** StockBalance.Issue so sánh lượng xuất với OnHand trừ Reserved. SQL CHECK và rowversion bảo vệ lần ghi. Hai yêu cầu xuất 8 khi tồn 10 cho một thành công và một xung đột trong InventoryTests.
- **Transaction bắt đầu ở đâu?** StockService.PostAsync mở transaction, đọc phiếu, kiểm tra số dư rồi lưu phiếu, số dư, ledger và audit trước commit.
- **Làm sao ngăn ghi sổ hai lần?** RequestId duy nhất bảo vệ tạo nháp. Phiếu đã ghi sổ trả lại số liệu hiện tại. Ledger chỉ có một dòng trên mỗi bộ phiếu, kho, sản phẩm.
- **Tính giá vốn thế nào?** StockBalance.Receive tính trung bình gia quyền và làm tròn 4 chữ số thập phân. StockMovement lưu giá vốn từng lần ghi sổ. Chuyển kho dùng đơn giá xuất của kho nguồn.
- **Đảo phiếu với điều kiện nào?** Chứng từ gốc phải còn là biến động mới nhất trên từng vị trí bị tác động. Chứng từ đảo khôi phục giá vốn trước đó và tham chiếu phiếu gốc.
- **Làm sao phân quyền và thu hồi phiên?** Policy đọc quyền hiện hành từ Identity. Khi khóa hoặc đổi quyền tài khoản, security stamp mới làm JWT và refresh phiên cũ mất hiệu lực.
- **Index dựa vào đâu?** SKU duy nhất cần cả tra cứu và chặn trùng. Ledger kiểm tra chuyển động gần nhất bằng thứ tự kho, sản phẩm và ID.
- **Chống SQL injection thế nào?** EF tạo tham số cho truy vấn LINQ. Dapper truyền tham số của procedure. Sort dùng danh sách cột cho phép.
- **Kiểm thử nghiệp vụ ở đâu?** InventoryTests chạy qua WebApplicationFactory và MiniErp_Test. Playwright dùng trình duyệt thật và API thật. StockBalanceTests kiểm tra công thức.
- **Khó khăn thực tế?** Windows khóa DLL của Kestrel. Chạy từ thư mục .work/api-run giúp build được khi phiên xem trước còn chạy.
- **Phần cần mở rộng?** Đơn mua, đơn bán, giữ hàng theo đơn, hóa đơn và công nợ sẽ nối vào chứng từ kho ở milestone sau.

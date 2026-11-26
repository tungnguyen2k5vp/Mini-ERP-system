# Bàn giao MiniErp

## Checkpoint V2 · Milestone 1

Đọc [kế hoạch V2](UPGRADE_V2_PLAN.md), [quyền truy cập](API_PERMISSION_MATRIX.md) và [kết quả chạy](TEST_REPORT.md). Migration thứ năm thêm giá vốn theo dòng bán. Đối soát trực tiếp trên bản thử tại `/bao-cao?view=reconciliation` và `/tai-chinh?tab=reconciliation`. Khi tiếp tục, lấy milestone 2 làm điểm bắt đầu.

Bản thử V2 trên máy này dùng `MiniErp_Test_V2_Browser`. Lệnh PowerShell để mở lại đúng bản đã diễn tập sau khi build:

~~~powershell
$env:MINIERP_TEST_CONNECTION = 'Server=lpc:localhost;Database=MiniErp_Test_V2_Browser;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
./scripts/start-dev.ps1 -TestDatabase
~~~

Khi bản thử đang chạy, dùng `./scripts/stop-dev.ps1` trước khi khởi động lại. Tài khoản Manager thử riêng được lưu trong `.work/v2-review-account.json` trên máy này. Các file trong `.work` được Git bỏ qua.

Ngày 26/11/2026. Mở http://localhost:5186, đăng nhập bằng `admin@minierp.example`. PowerShell trong thư mục MiniErp hiển thị riêng mật khẩu demo bằng `(Get-Content .work/dev.env.json -Raw | ConvertFrom-Json).AdminPassword`. File này được Git bỏ qua.

## Chạy và kiểm tra

Chạy lần lượt `./scripts/setup.ps1`, `./scripts/migrate-db.ps1`, `./scripts/seed-demo.ps1`, `./scripts/start-dev.ps1`. API tại http://127.0.0.1:5168, kiểm tra `/health/ready`. Nếu cần khởi động lại dùng `./scripts/stop-dev.ps1`. Chạy `./scripts/test-all.ps1 -Browser` khi dịch vụ đã sẵn sàng. Integration tests dùng MiniErp_Test. Playwright thêm chứng từ mẫu vào database của bản xem trước. [Triển khai và CI](DEPLOYMENT.md)

## Những luồng đã nối

- Mua hàng, duyệt, nhận một phần, trả hàng và ghi nợ nhà cung cấp. Bán hàng, giữ hàng khi duyệt, giao một phần, huỷ phần còn lại, phải thu và hoàn tiền theo số tiền đã thực thu.
- Từ đơn có thể theo đến phiếu kho và đối soát hóa đơn. Mỗi lần ghi sổ cập nhật kho, đơn, chi phí và hóa đơn trong một giao dịch SQL. Sửa sai qua chứng từ đảo hoặc chứng từ trả hàng.
- Dashboard theo quyền, báo cáo tồn theo kỳ, tuổi nợ theo thời điểm, đối soát, tìm kiếm, nhật ký, nhập/xuất Excel và bản in A4. Tài chính phục vụ vận hành nội bộ.
- Seed có kho mô phỏng riêng, 300 đơn trong sáu tháng và cả công nợ quá hạn. Bảy vai trò có sẵn gồm Administrator, Manager, Sales Staff, Purchasing Staff, Warehouse Staff, Accountant và Viewer. Tạo tài khoản nhân viên tại **Quản trị hệ thống → Người dùng**.

## Tiếp tục phiên mới

Đọc AGENTS.md, sau đó đọc [trạng thái](PROJECT_STATUS.md), [các quyết định](DECISIONS.md), [kết quả kiểm thử](TEST_REPORT.md) và [việc còn lại](NEXT_STEPS.md). Source chính là OrderService, StockService, FinanceService, Settlement và OperationsReport. EF quản lý năm migration đến hết milestone 1 của V2.

[Demo từng bước](DEMO_GUIDE.md) · [Báo cáo SQL](SQL_PERFORMANCE.md) · [Bài tập và phỏng vấn](interview/README.md)

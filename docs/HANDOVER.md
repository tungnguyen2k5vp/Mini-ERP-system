# Bàn giao MiniErp

## Mốc 3 · Hạn mức và sửa sai phiếu thu chi · 27/11/2026

Migration thứ bảy là `20261127041408_CreditAndPaymentCorrections`. Khi duyệt đơn bán, hệ thống khóa khách hàng, cộng nợ hóa đơn và cam kết chưa giao, rồi so với hạn mức. Hạn mức bằng 0 là không giới hạn. Duyệt vượt mức cần quyền riêng và lý do. Phiếu đảo có liên kết tới phiếu thu, chi hoặc hoàn tiền gốc. Sao kê áp dụng bút toán đảo từ lúc ghi sổ. [Chính sách tín dụng và thu chi](FINANCE.md) · [Ma trận quyền](API_PERMISSION_MATRIX.md)

Mở **Công nợ & thanh toán → Lịch sử đối tác**, chọn **Công ty Phân phối Minh An** (`DEMO-TD`). Đơn bán 600.000 đồng đã giao 480.000 đồng. Trong sao kê có phiếu thu nhầm 180.000 đồng, phiếu đảo và khoản thu đúng 120.000 đồng. Dư hóa đơn là 360.000 đồng, cam kết chưa giao 120.000 đồng, còn 20.000 đồng trên hạn mức 500.000 đồng. Chọn liên kết đơn trong sao kê để xem lần duyệt ngoại lệ và phiếu giao gốc. [Kịch bản demo](DEMO_GUIDE.md)

Đã chạy 14 unit test, 75 ca tích hợp SQL Server, 3 ca Vitest và 44 ca Playwright. Hai lần diễn tập nâng cấp giữ nguyên dấu vân tay lịch sử. Bản cài mới và bản nâng cấp đều có số dư và giá vốn theo thứ tự giao dịch khớp sau hai lần chạy seed. [Báo cáo kiểm thử và ảnh](TEST_REPORT.md)

## Chạy bản thử trên máy này

~~~powershell
$env:MINIERP_TEST_CONNECTION = 'Server=lpc:localhost;Database=MiniErp_Test_M3_Preview_20261127;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
./scripts/start-dev.ps1 -TestDatabase
~~~

Khi bản thử đang chạy, dùng `./scripts/stop-dev.ps1` trước khi khởi động lại. Tài khoản Manager thử riêng được lưu trong `.work/v2-review-account.json` trên máy này. Các file trong `.work` được Git bỏ qua.

Mở http://localhost:5186, đăng nhập bằng `admin@minierp.example`. Trong PowerShell tại thư mục MiniErp, lấy mật khẩu bằng `(Get-Content .work/dev.env.json -Raw | ConvertFrom-Json).AdminPassword`. Administrator thử được cả duyệt vượt mức và đảo phiếu. Manager có thể duyệt đơn do người khác lập trong hạn mức và đọc sao kê.

## Chạy và kiểm tra

Chạy lần lượt `./scripts/setup.ps1`, `./scripts/migrate-db.ps1`, `./scripts/seed-demo.ps1`, `./scripts/start-dev.ps1`. API tại http://127.0.0.1:5168, kiểm tra `/health/ready`. Nếu cần khởi động lại dùng `./scripts/stop-dev.ps1`. Chạy `./scripts/test-all.ps1 -Browser` khi dịch vụ đã sẵn sàng. Integration tests dùng MiniErp_Test. Playwright thêm chứng từ mẫu vào database của bản xem trước. [Triển khai và CI](DEPLOYMENT.md)

## Những luồng đã nối

- Mua hàng, duyệt, nhận một phần, trả hàng và ghi nợ nhà cung cấp. Bán hàng, giữ hàng khi duyệt, giao một phần, huỷ phần còn lại, phải thu và hoàn tiền theo số tiền đã thực thu.
- Từ đơn có thể theo đến phiếu kho và đối soát hóa đơn. Mỗi lần ghi sổ cập nhật kho, đơn, chi phí và hóa đơn trong một giao dịch SQL. Sửa sai qua chứng từ đảo hoặc chứng từ trả hàng.
- Dashboard theo quyền, báo cáo tồn theo kỳ, tuổi nợ theo thời điểm, đối soát, tìm kiếm, nhật ký, nhập/xuất Excel và bản in A4. Tài chính phục vụ vận hành nội bộ.
- Seed có kho mô phỏng riêng, 300 đơn trong sáu tháng và cả công nợ quá hạn. Bảy vai trò có sẵn gồm Administrator, Manager, Sales Staff, Purchasing Staff, Warehouse Staff, Accountant và Viewer. Tạo tài khoản nhân viên tại **Quản trị hệ thống → Người dùng**.

## Tiếp tục phiên mới

Đọc AGENTS.md, sau đó đọc [trạng thái](PROJECT_STATUS.md), [các quyết định](DECISIONS.md), [kết quả kiểm thử](TEST_REPORT.md) và [việc còn lại](NEXT_STEPS.md). Mốc 4 triển khai báo giá, bảng giá và chiết khấu. Chốt cách phân bổ số lẻ khi giao từng phần, trả hàng và đảo trước khi nối báo giá với đơn bán.

[Demo từng bước](DEMO_GUIDE.md) · [Báo cáo SQL](SQL_PERFORMANCE.md) · [Bài tập và phỏng vấn](interview/README.md)

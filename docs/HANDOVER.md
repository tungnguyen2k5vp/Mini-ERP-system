# Bàn giao MiniErp

Ngày 25/11/2026. Mã nguồn nằm tại D:/ReactJS/MiniErp. Trạng thái từng milestone và các điểm chưa xác minh có trong [tiến độ](PROJECT_STATUS.md) và [vấn đề còn lại](KNOWN_ISSUES.md).

## Chạy tại máy

Ứng dụng dùng [http://localhost:5186](http://localhost:5186). API lắng nghe trên http://127.0.0.1:5168. Tài liệu API tại [OpenAPI v1](http://127.0.0.1:5168/openapi/v1.json). Email demo là admin@minierp.example. Lấy mật khẩu tự sinh từ .work/dev.env.json trong thư mục dự án.

Chạy các lệnh sau từ D:/ReactJS/MiniErp để khởi tạo lại môi trường phát triển.

~~~powershell
.\scripts\setup.ps1
.\scripts\migrate-db.ps1
.\scripts\seed-demo.ps1
.\scripts\start-dev.ps1
~~~

PowerShell kết nối SQL Server Developer 2022 qua lpc:localhost và Windows Authentication. MiniErp dành cho demo. MiniErp_Test dành cho xUnit và WebApplicationFactory. migrate-db áp dụng migrations, seed-demo tạo dữ liệu thử và các phiếu tồn đầu kỳ có thể chạy lại. Cấu hình instance khác và lệnh chạy API riêng có trong [triển khai](DEPLOYMENT.md).

## Kiến trúc và phiên bản

- Modular Monolith gồm Domain, Application, Infrastructure và ASP.NET Core API. Giao diện React gọi /api/v1. [Kiến trúc](architecture/ARCHITECTURE.md) [ERD](diagrams/ERD.md)
- SDK .NET 10.0.102 và runtime 10.0.2 trên máy kiểm thử. EF Core 10.0.2, Dapper 2.1.66, SqlClient 6.1.1 và SQL Server 2022 Developer 16.0.1000.6.
- Node 24.11.1, npm 11.6.2, React 19.3.1, TypeScript 5.9.3, Vite 7.3.8 và Tailwind 4.3.5. [Phiên bản và nguồn tham khảo](REPOSITORY_REFERENCES.md)

## Phạm vi bàn giao

| Đã triển khai | Phạm vi |
| --- | --- |
| Xác thực và phân quyền | Identity, JWT, refresh, khóa tài khoản, hồ sơ, người dùng, vai trò và quyền. |
| Danh mục và đối tác | Sản phẩm, nhóm hàng, đơn vị, kho, phương thức thanh toán, khách hàng và nhà cung cấp. |
| Kho | Nhập, xuất, chuyển, điều chỉnh, kiểm kê, hủy nháp, đảo ghi sổ, lịch sử và báo cáo tồn. Giá vốn trung bình động được tính theo vị trí kho và sản phẩm. |
| Dữ liệu demo | 104 sản phẩm, 8 nhóm, 3 kho, 30 khách hàng, 15 nhà cung cấp và 96 vị trí tồn đầu kỳ. |

Các luồng mua hàng và bán hàng, giữ hàng theo đơn, tài chính, báo cáo tổng hợp, Excel, Docker, CI và bộ câu hỏi thực hành đầy đủ thuộc các milestone tiếp theo. [Việc tiếp theo](NEXT_STEPS.md)

Ứng dụng xem trước đang dùng các dependency đã qua lượt kiểm thử. Lockfile cuối đã qua npm ci dry-run. Lượt cài sạch và chạy lại toàn bộ frontend theo lockfile cuối chưa được xác minh. [Chi tiết kiểm thử](TEST_REPORT.md)

## Kiểm chứng và học

- Build, migrations, kết quả kiểm thử và giới hạn xác minh có trong [báo cáo kiểm thử](TEST_REPORT.md).
- Ảnh chụp giao diện thật [tồn kho desktop](screenshots/inventory-desktop.png), [tồn kho mobile](screenshots/inventory-mobile.png), [phiếu chuyển kho](screenshots/stock-transfer-desktop.png) và [sản phẩm desktop](screenshots/products-desktop.png).
- Mười skill .NET chính thức cài tại D:/ReactJS/.agents/skills/dotnet. Tài liệu ghi các skill của workspace đã áp dụng và kiểm tra khả năng Codex phát hiện skill lồng nhau. [Kho skill](SKILLS_INVENTORY.md) [Skill đã áp dụng](SKILLS_USAGE.md)
- Có kịch bản chuyển kho, diễn giải quyết định, bullet cho CV và lộ trình đọc code. [Demo](DEMO_GUIDE.md) [CV](interview/CV_PROJECT.md) [Giải thích thiết kế](interview/PROJECT_DEFENSE.md) [Lộ trình 7 ngày](interview/LEARNING_GUIDE.md)

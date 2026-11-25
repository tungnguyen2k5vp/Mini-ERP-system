# MiniErp

Ứng dụng quản lý vận hành cho doanh nghiệp phân phối, dùng ASP.NET Core 10, SQL Server và React. Checkpoint hiện tại có đăng nhập, phân quyền, danh mục, đối tác và các chứng từ kho nhập, xuất, chuyển, điều chỉnh, kiểm kê. Xem [bàn giao](docs/HANDOVER.md) để biết phần đã chạy và việc còn lại.

Trong D:/ReactJS/MiniErp, chạy:

~~~powershell
.\scripts\setup.ps1
.\scripts\migrate-db.ps1
.\scripts\seed-demo.ps1
.\scripts\start-dev.ps1
~~~

Truy cập [MiniErp](http://localhost:5186). Email demo là admin@minierp.example. Mật khẩu được sinh riêng khi khởi tạo và lưu trong .work/dev.env.json. Dùng scripts/stop-dev.ps1 để dừng tiến trình của dự án.

Xem [README tiếng Anh](README.md), [hướng dẫn demo](docs/DEMO_GUIDE.md), [báo cáo kiểm thử](docs/TEST_REPORT.md) và [lộ trình đọc code](docs/interview/LEARNING_GUIDE.md).

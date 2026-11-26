# Demo guide

Run the local setup, migrations, seed and start scripts from the [README](../README.md). Sign in as the seeded administrator and use the local password in .work/dev.env.json. The seed records are fictitious. Inventory opening balances are posted by the same stock service used by the API.

## Review a transfer

1. Open Tồn kho and search VP-01-02. Confirm the positions and available quantities.
2. Under Chứng từ kho, choose Lập phiếu and select Chuyển kho.
3. Choose Kho Hà Nội and Kho Hồ Chí Minh, add VP-01-02, enter 3 and save the draft.
4. Ghi sổ the draft. Open Biến động kho and find two rows from the same document, with -3 and +3.
5. Return to the transfer and choose Đảo chứng từ before creating another movement on the affected positions. Check that the two warehouse quantities return to their previous values.

## Mua hàng và công nợ phải trả

1. Trong Đơn mua, tạo đơn 10 đơn vị, đơn giá 100.000 đồng cho một nhà cung cấp. Ghi lại mã PO và kho nhận.
2. Gửi duyệt và phê duyệt. Nhận 4 đơn vị, rồi mở phiếu kho từ lịch sử của PO. Kho tăng 4, hóa đơn phải trả bằng 400.000 đồng và 6 đơn vị còn chờ nhận.
3. Nhận 6 đơn vị còn lại. Trên Tài chính, lọc đúng nhà cung cấp và chiều phải trả. Phân bổ 200.000 đồng cho hóa đơn đầu. Tổng còn phải trả cho riêng đơn này bằng 800.000 đồng.
4. Trả 1 đơn vị. Mở lại công nợ và kiểm tra chứng từ giảm 100.000 đồng. Đối soát chênh lệch bằng 0.

## Bán hàng, giữ hàng và thu tiền

1. Dùng kho và SKU vừa nhập. Tạo đơn bán 5 đơn vị, đơn giá 150.000 đồng. Sau khi duyệt, lượng giữ tăng 5, tồn thực tế giữ nguyên.
2. Giao 2 đơn vị. Lượng giữ còn 3, tồn thực tế giảm 2 và hóa đơn phải thu bằng 300.000 đồng. Mở phiếu xuất để xem giá vốn ghi tại thời điểm giao.
3. Thu 120.000 đồng cho hóa đơn này. Hủy phần chưa giao để giải phóng 3 đơn vị giữ. Hóa đơn và khoản còn phải thu 180.000 đồng vẫn tra cứu được.
4. Trên tổng quan, chọn kỳ có ngày giao và kiểm tra khoản doanh thu của lần giao. Báo cáo kho cho biết tồn đầu, nhập, xuất và tồn cuối tại kho đã chọn.

## Bộ lịch sử mẫu

Kho mô phỏng phân phối dùng vị trí riêng cho lịch sử sáu tháng. Seed tạo 100 PO, 200 SO, các lần nhận/giao từng phần, trả hàng và thanh toán từng phần. Có thể tra mã PO/SO từ tìm kiếm toàn hệ thống, in chi tiết hoặc lọc tuổi nợ theo ngày. Dữ liệu của người dùng ở kho khác được giữ nguyên.

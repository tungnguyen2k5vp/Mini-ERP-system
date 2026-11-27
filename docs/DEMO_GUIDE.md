# Demo guide

## Milestone 3 · Ví dụ tín dụng và sửa thu chi

Mở **Công nợ & thanh toán → Lịch sử đối tác**, chọn **Công ty Phân phối Minh An**. Đơn bán 600.000 đồng đã được duyệt vượt hạn mức 500.000 đồng với lý do lưu trên đơn. **Kho mô phỏng tín dụng** (`DEMO-TD`) giao 8 trong 10 sản phẩm. Sao kê ghi hóa đơn 480.000 đồng, phiếu thu nhầm 180.000 đồng, lần đảo và khoản thu đúng 120.000 đồng. Dư hóa đơn là 360.000 đồng, cam kết chưa giao là 120.000 đồng, còn 20.000 đồng hạn mức. Mở liên kết đơn nguồn để đối chiếu lịch sử duyệt và giao hàng. [Mở sao kê trên bản thử](http://localhost:5186/tai-chinh?tab=statement&direction=1&partnerId=fb883000-a43b-4b17-9dfa-000000000001)

1. Tạo khách hàng hạn mức 1.000.000 đồng và đơn bán 600.000 đồng. Duyệt đơn, xem cam kết chưa giao 600.000; giao 200.000 rồi xem công nợ 200.000 cộng cam kết 400.000, tổng sử dụng vẫn 600.000. Đơn mới 400.000 đưa dự kiến đúng hạn mức; đơn 500.000 cần quyền vượt hạn mức và lý do. Khách hàng hạn mức 0 không bị chặn bởi hạn mức.
2. Mở sao kê chốt ngày trước khi giao và ngày sau khi giao. So sánh với khối tín dụng hiện tại: sao kê không cộng cam kết chưa giao. Kiểm tra cùng một phiếu thu phân bổ hai hóa đơn xuất hiện thành hai dòng theo đúng số phân bổ, không lặp toàn bộ số tiền phiếu.
3. Với đơn có hóa đơn 100.000 đồng, thu đủ 100.000 rồi ghi giảm 40.000 và hoàn 40.000. Thử đảo phiếu thu: bị chặn vì tiền hoàn còn hiệu lực. Người có đủ ba quyền tài chính đảo phiếu hoàn với lý do, sau đó đảo phiếu thu. Phiếu và phân bổ gốc vẫn tra cứu được; muốn sửa phân bổ thì tạo phiếu mới.
4. Chốt sao kê trước và sau lần đảo. Trước mốc đảo vẫn có tác động thu/hoàn gốc; sau mốc đảo có dòng ngược dấu. Kiểm tra dư cuối và quá hạn trên toàn bộ kỳ, không chỉ trang đang mở. Gửi lại đúng requestId/lý do bằng cùng người thực hiện trả lần đảo cũ.

`Finance.Reverse` và `Sales.OverrideCreditLimit` mặc định chỉ dành cho quản trị viên nhưng có thể cấp riêng theo vai trò. Đây là hướng dẫn thao tác; kết quả chạy thực tế được ghi riêng trong báo cáo kiểm thử.

## V2 walkthrough · 12 minutes

| Time | Live task | What to check |
| --- | --- | --- |
| 0–2 min | Open the Manager dashboard, select a Vietnam business period, then follow the stock or aging metric. | The drill-down keeps the selected dates. Stock counts distinct SKUs and the daily chart counts receipt and issue documents. |
| 2–5 min | Create a small purchase and sales order using the existing steps below. Ship partially, then inspect stock reconciliation for the selected warehouse. | A healthy location has no exceptions. The shipment reduced both available reservation and physical stock by the quantities appropriate to each record. |
| 5–8 min | Return part of a newly shipped sales line and inspect the linked invoice and stock document. | The goods return has a separate credit. Refunding cash requires prior payment. The final return for a SKU clears its remaining recorded cost. |
| 8–10 min | Compare aging and source exceptions for the same closing date. | Follow the source IDs of any actual exception. On healthy demo data the exception list is empty. The rollback-only regression demonstrates data discrepancies on isolated test records. |
| 10–12 min | Open an existing purchase order as Warehouse Staff and inspect its quantities and print layout. | Historical purchase prices stay restricted. The Manager session can inspect amounts using the same order ID. |

Run the local setup, migrations, seed and start scripts from the [README](../README.md). Sign in as the seeded administrator and use the local password in .work/dev.env.json. The seed records are fictitious. Inventory opening balances are posted by the same stock service used by the API.

## Review a transfer

1. Open Tồn kho and search VP-01-02. Confirm the positions and available quantities.
2. Under Chứng từ kho, choose Lập phiếu and select Chuyển kho.
3. Choose Kho Hà Nội and Kho Hồ Chí Minh, add VP-01-02, enter 3 and save the draft.
4. Ghi sổ the draft. Open Biến động kho and find two rows from the same document, with -3 and +3.
5. Return to the transfer and choose Đảo chứng từ before creating another movement on the affected positions. Check that the two warehouse quantities return to their previous values.

## Mua hàng và công nợ phải trả

1. Trong Đơn mua, tạo đơn 10 đơn vị, đơn giá 100.000 đồng cho một nhà cung cấp. Ghi lại mã PO và kho nhận.
2. Gửi duyệt và dùng người duyệt độc lập (hoặc quản trị viên có quyền ngoại lệ). Nhận 4 đơn vị, rồi mở phiếu kho từ lịch sử của PO. Kho tăng 4, hóa đơn phải trả bằng 400.000 đồng và 6 đơn vị còn chờ nhận.
3. Nhận 6 đơn vị còn lại. Trên Tài chính, lọc đúng nhà cung cấp và chiều phải trả. Phân bổ 200.000 đồng cho hóa đơn đầu. Tổng còn phải trả cho riêng đơn này bằng 800.000 đồng.
4. Trả 1 đơn vị. Mở lại công nợ và kiểm tra chứng từ giảm 100.000 đồng. Đối soát chênh lệch bằng 0.

## Bán hàng, giữ hàng và thu tiền

1. Dùng kho và SKU vừa nhập. Tạo đơn bán 5 đơn vị, đơn giá 150.000 đồng. Sau khi duyệt, lượng giữ tăng 5, tồn thực tế giữ nguyên.
2. Giao 2 đơn vị. Lượng giữ còn 3, tồn thực tế giảm 2 và hóa đơn phải thu bằng 300.000 đồng. Mở phiếu xuất để xem giá vốn ghi tại thời điểm giao.
3. Thu 120.000 đồng cho hóa đơn này. Hủy phần chưa giao để giải phóng 3 đơn vị giữ. Hóa đơn và khoản còn phải thu 180.000 đồng vẫn tra cứu được.
4. Trên tổng quan, chọn kỳ có ngày giao và kiểm tra khoản doanh thu của lần giao. Báo cáo kho cho biết tồn đầu, nhập, xuất và tồn cuối tại kho đã chọn.

## Bộ lịch sử mẫu

Kho mô phỏng phân phối dùng vị trí riêng cho lịch sử sáu tháng. Seed tạo 100 PO, 200 SO, các lần nhận/giao từng phần, trả hàng và thanh toán từng phần. Có thể tra mã PO/SO từ tìm kiếm toàn hệ thống, in chi tiết hoặc lọc tuổi nợ theo ngày. Dữ liệu của người dùng ở kho khác được giữ nguyên.

## Thử quy trình nháp và phê duyệt V2

1. Đăng nhập bằng tài khoản lập đơn. Vào **Đơn mua hàng**, tạo đơn nháp có ngày nhận dự kiến và mở đơn ở hai thẻ. Sửa số lượng rồi lưu ở thẻ thứ nhất. Lưu ở thẻ còn lại để thấy cảnh báo xung đột. Nội dung đang nhập được giữ tới khi bạn xác nhận bỏ thay đổi và tải bản mới.
2. Gửi duyệt. Trong một cửa sổ riêng tư, đăng nhập bằng tài khoản **Manager** khác người lập. Chọn **Yêu cầu sửa** và nhập lý do. Quay lại tài khoản lập đơn, sửa nội dung, lưu rồi gửi lại. Dùng tài khoản quản lý để duyệt.
3. Mở **Lịch sử phê duyệt**, đối chiếu thứ tự thao tác, người xử lý, thời điểm và lý do. Thử **Từ chối** trên một đơn chờ duyệt khác để xem trạng thái kết thúc. Nhật ký cũ thiếu danh tính hiển thị “Chưa rõ người thực hiện”.
4. Với vai trò **Warehouse Staff**, mở một phiếu nhập nháp và sửa số lượng. Sản phẩm giữ lại dùng giá vốn đã lưu. Khi thay sản phẩm, máy chủ lấy giá mua của sản phẩm mới. Với phiếu kiểm kê, nhập số đếm mới và đánh dấu xác nhận đã đếm lại trước khi lưu.
5. Ghi sổ phiếu vừa sửa. Gửi lại đúng yêu cầu ghi sổ với phiên bản ban đầu để kiểm tra số biến động kho. Thử hủy một phiếu nháp khác, nhập lý do và xem lại lịch sử.
6. Lọc đơn quá hạn để xem các đơn đã duyệt hoặc giao/nhận một phần có ngày dự kiến trước hôm nay theo giờ Việt Nam. Hủy phần chưa giao của một đơn bán, rồi đối chiếu lượng hàng được giải phóng và công nợ của phần đã giao.

Theo quyền mặc định, Manager có thể duyệt chéo. Người lập, người sửa gần nhất hoặc người gửi đơn cần thêm `Orders.SelfApprove` để tự duyệt. Nếu có cấu hình ngưỡng tiền, tổng đơn từ đúng ngưỡng trở lên còn cần `Orders.ApproveAboveLimit`. [Quyền và ngưỡng duyệt](API_PERMISSION_MATRIX.md)

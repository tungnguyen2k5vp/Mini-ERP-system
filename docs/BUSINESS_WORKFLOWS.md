# Business workflows

## V3 · Tín dụng, sao kê và sửa phiếu thu chi

Trước khi duyệt đơn bán, xem **Tín dụng hiện tại**: công nợ thuần dương theo từng đơn cộng cam kết đã duyệt nhưng chưa giao. Giao hàng chuyển cam kết sang công nợ hóa đơn, không cộng hai lần. Hạn mức 0 là không giới hạn; dự kiến bằng hạn mức vẫn hợp lệ. Vượt hạn mức dương cần quyền `Sales.OverrideCreditLimit`, lý do và các quyền duyệt hiện hành.

Sao kê chọn đối tác và ngày chốt Việt Nam. Đây là ảnh chụp công nợ tại mốc chốt, còn tín dụng luôn là số hiện tại và có thêm cam kết chưa lập hóa đơn. Một phiếu phân bổ nhiều hóa đơn có một dòng cho mỗi phân bổ; số tiền có dấu theo tác động lên nợ. Bù trừ hóa đơn giảm với hóa đơn cũ nhất trong cùng đơn/chiều công nợ. Dư cuối kỳ và quá hạn không phụ thuộc trang đang mở.

Phiếu thu chi sai được đảo toàn bộ bằng `Finance.Reverse` cùng `Finance.Post` và `Finance.View`. Nhập lý do; hệ thống giữ nguyên phiếu và phân bổ gốc, thêm lần đảo liên kết. Nếu còn tiền hoàn phụ thuộc khiến tiền thực của một đơn bị âm, đảo phiếu hoàn trước rồi mới đảo phiếu thu/chi. Không đảo dây chuyền tự động. Sau đó lập phiếu mới nếu cần phân bổ lại. Thời điểm ghi không được lùi trước hóa đơn, thanh toán hoặc lần đảo liên quan; sao kê trước lần đảo vẫn phản ánh phiếu gốc.

## Tracing discrepancies

Open **Báo cáo tồn kho → Đối soát hiện tại**, optionally choose a warehouse, then open a reported SKU to inspect that warehouse's movement history. Compare the ledger with on-hand, saved reservations and the remaining approved quantity. The inconsistent-line count also catches opposite errors that cancel in a position total.

Open **Công nợ & thanh toán → Đối soát**, choose receivables or payables and the closing business date. The first table compares accounting totals. The exceptions list checks individual fulfillment, invoice and allocation sources. Open a permitted source document from a row to investigate. A historical correction still uses the normal return, reversal or settlement workflow.

## Access

Sign in with a provisioned email and password. Administrators can create employees, assign roles and lock accounts. The API checks current permissions and records access changes. The personal profile shows current role grants and supports password changes.

## Master data

Create a category and unit before adding a product. Use the dedicated customer and supplier pages to record contacts and terms. Codes are unique by resource type and SKU is unique across products. Opening a record provides its current version. Saving a stale version prompts the operator to reload. Deactivation retains references from existing documents.

## Warehouse

Choose a document type and an active warehouse. Use distinct source and destination warehouses for a transfer. Adding a draft leaves stock unchanged. Post the draft to save the balance and ledger atomically. A posted issue consumes available stock. An inbound receipt updates moving-average cost and a transfer carries the source issue cost. An adjustment records a signed difference. A count records the physical count and checks the balance version captured when the draft was created.

A posted document remains in the ledger. To reverse it, open its detail and choose Đảo chứng từ. The API rejects reversal when later ledger activity exists for an affected balance. Successful reversal adds an inverse document, restores the prior average cost and links both records. Reposting an already posted document does not modify balances again.

## Orders and settlement

The order snapshots the agreed quantity and unit price per product. Submission and approval use the current order version. Sales approval reserves quantities per line. Partial shipment releases only what ships. Cancellation releases the remainder. Receipt or shipment checks the unfulfilled quantity and writes the order line, stock movement, balance, invoice and audit in a single database transaction.

A return is capped at the net quantity delivered and creates its own stock movement and credit. A correction reverses the latest eligible posting through its source order. Previous invoice and cash records remain available. See [settlement rules](FINANCE.md) for cash limits, credit offsets, refunds and aging.

## V2 · Quy trình duyệt và sửa chứng từ

| Chứng từ và trạng thái | Thao tác hợp lệ | Kết quả |
| --- | --- | --- |
| Đơn nháp | Sửa, gửi duyệt, hủy | Nháp, chờ duyệt, đã hủy |
| Đơn chờ duyệt | Duyệt, trả để sửa, từ chối, hủy | Đã duyệt, cần chỉnh sửa, bị từ chối, đã hủy |
| Đơn cần chỉnh sửa | Sửa nội dung, hủy | Nháp sau khi lưu thay đổi, hoặc đã hủy |
| Đơn đã duyệt | Nhận/giao hàng, hủy phần chưa thực hiện | Một phần, hoàn thành, hoặc đã hủy |
| Đơn thực hiện một phần | Nhận/giao tiếp, hủy phần còn lại | Một phần, hoàn thành, hoặc đã hủy và giải phóng phần giữ còn lại |
| Đơn hoàn thành hoặc đã hủy sau khi giao/nhận | Trả hàng hoặc đảo chứng từ đủ điều kiện | Ghi chứng từ và bút toán liên kết với nguồn |
| Đơn bị từ chối | Xem lịch sử | Kết thúc xét duyệt |
| Phiếu kho nháp độc lập | Sửa, ghi sổ, hủy | Nháp, đã ghi sổ, đã hủy |
| Phiếu kho đã ghi sổ | Đảo phiếu đủ điều kiện | Chứng từ đảo liên kết với bản gốc |
| Phiếu kho đã đảo hoặc đã hủy | Xem lịch sử | Kết thúc |

Các thao tác sửa, gửi và xét duyệt dùng phiên bản người dùng đang xem. Trả để sửa, từ chối và hủy phải ghi lý do. Việc duyệt đơn bán giữ hàng trong cùng giao dịch. Hủy đơn đã giao một phần giữ nguyên phiếu kho và hóa đơn đã ghi sổ.

Quyền duyệt theo loại đơn là điều kiện đầu tiên. Đơn do chính người duyệt lập, sửa nội dung gần nhất hoặc gửi duyệt cần thêm quyền `Orders.SelfApprove`. Với chứng từ cũ thiếu người lập trong nhật ký, quyền này cũng bắt buộc. Quản trị viên có quyền này theo cơ chế cấp quyền hiện hành. Các vai trò nghiệp vụ mặc định thực hiện duyệt chéo. Nếu doanh nghiệp cấu hình `Orders__ApprovalThresholdVnd`, tổng tiền từ đúng ngưỡng trở lên còn yêu cầu `Orders.ApproveAboveLimit`. Bỏ trống cấu hình sẽ chỉ xét quyền theo loại đơn và tác giả.

Ngày dự kiến là ngày nghiệp vụ Việt Nam. Đơn đã duyệt hoặc thực hiện một phần được cảnh báo trễ từ đầu ngày kế tiếp. Nhật ký dùng UTC và hiển thị người thực hiện theo tài khoản đã ghi nhận.

# Operational finance

Each posted order fulfillment, return or reversal creates one immutable internal invoice/credit linked to its order and stock document. Amounts use two decimal places, rounding midpoint away from zero; partial fulfillment/return totals use cumulative line deltas so full quantities reconcile to their order price snapshots. Sales cost comes from the posted stock ledger. This module does not implement statutory accounting or official electronic invoices.

## Settlement and returns

Payments must allocate their entire positive amount to one or more invoices for the same partner and receivable/payable direction. A request identifier is mandatory; retrying the same normalized payload returns the original payment, while reusing its identifier with a different payload conflicts. Refunds allocate credit invoices and require actual cash previously paid against their source order.

Unpaid returns automatically offset the oldest unpaid invoices **within the same order and direction**. The offset is derived from immutable source documents and cash allocations; it does not create a cash payment or rewrite invoice amounts. The invoice API exposes:

| Field | Meaning |
| --- | --- |
| `settled` | Actual cash allocated to this invoice/credit. |
| `offsetAmount` | Same-order debit/credit offset, allocated by ascending invoice ID. |
| `outstanding` | `amount - settled - offsetAmount`; signed credit balances subtract in debt reports. |
| `actionableAmount` | Maximum cash allocation/refund currently available on this invoice. Refunds are additionally limited to actual net cash already received/paid for the source order. |

For debit 100 and return credit 40 before cash, only 60 is collectible and no refund is available. Paying that 60 closes both source documents. Returning all 100 before payment closes both without a receipt or refund. If a later correction makes net invoice value negative, the extra credit remains visible but cannot create a cash refund larger than cash actually received.

Payment and payment-correction writes lock the partner before source orders; multi-order operations lock orders in ascending ID using `UPDLOCK,HOLDLOCK`. Sales approval and related order writes use the same partner-before-order boundary. Payment/allocations/audit or reversal/audit commit together. Conflicts roll back entirely; reuse the same request identifier after an ambiguous response.

The aging report applies the same credit offsets using only invoices and cash allocations present at its historical UTC cutoff. Reconciliation independently compares signed invoice/credit totals, payment/refund totals and signed outstanding balances. No balances or revenue are inferred from draft orders.

## Verification

`SettlementTests` covers unpaid partial/full returns, negative net invoice values, cash-backed refunds and multiple source documents. `FinanceTests` covers the API, semantic retries, overpayment, net settlement, refunds before cash, competing same-invoice/multi-order allocations, concurrent return/payment, permissions and reports. `ReportingTests` covers actual purchasing/sales/return source reconciliation and restricted dashboard/search visibility.

Actual execution results belong in the [test report](TEST_REPORT.md); the contracts below do not imply that verification has run.

## Milestone 3: tín dụng hiện tại và sao kê tại mốc chốt

Tín dụng hiện tại tính `used = outstanding + unbilled`. `outstanding` cộng phần công nợ thuần dương của từng đơn bán sau hóa đơn giảm và tiền thực còn hiệu lực; phần dư có của đơn này không tài trợ đơn khác. `unbilled` chỉ tính phần chưa giao của đơn đã duyệt/đang giao, bằng chênh lệch hai số tiền làm tròn lũy kế. Giao một phần chuyển giá trị từ cam kết sang hóa đơn, không cộng hai lần. Duyệt đơn mới cộng giá trị đơn vào `projected` dưới khóa khách hàng.

Hạn mức 0 là không giới hạn. Với hạn mức dương, chỉ khi dự kiến vượt hạn mức mới cần `Sales.OverrideCreditLimit`, quyền duyệt thông thường và lý do ghi nhật ký. Việc này độc lập với quyền tự duyệt và ngưỡng duyệt theo số tiền.

`Catalog.Create` cho phép đặt chính sách hạn mức ban đầu và lưu sự kiện riêng, bao gồm 0. Đổi hạn mức khách hàng đã tồn tại theo bất kỳ hướng nào cần `Finance.ChangeCreditLimit` cùng `Catalog.Update`; quyền mới mặc định chỉ quản trị viên có nhưng cấp được riêng theo vai trò. Sửa liên hệ giữ đúng hạn mức hiện tại không cần quyền này. Máy chủ kiểm tra giá trị đang lưu và phiên bản trước khi sửa, rồi ghi người thực hiện và giá trị trước/sau nguyên tử.

Sao kê chốt theo ngày Việt Nam hoặc thời điểm UTC. Mỗi phân bổ thu/chi xuất hiện trên một dòng hóa đơn nguồn; thu/chi nhiều hóa đơn không bị lặp toàn bộ giá trị phiếu. Hóa đơn tăng nợ là số dương, ghi giảm và thanh toán là số âm, hoàn tiền là số dương; lần đảo mang dấu ngược phiếu gốc. Bù trừ chỉ trong cùng đơn/chiều công nợ và theo hóa đơn cũ nhất. Dư cuối kỳ và quá hạn tính tại mốc chốt trên toàn bộ tập dữ liệu. Khối tín dụng hiển thị hiện tại có thể khác sao kê lịch sử vì còn bao gồm cam kết chưa xuất hóa đơn.

## Đảo thanh toán bất biến

Chỉ đảo toàn bộ một phiếu đã ghi, một lần; phiếu gốc và các phân bổ không đổi. Bản ghi đảo liên kết với phiếu gốc và lưu thời điểm, người thực hiện, lý do, requestId. Không có đảo một phần hoặc đảo bản ghi đảo. Muốn phân bổ lại, đảo phiếu cũ rồi tạo phiếu mới theo quy tắc thanh toán hiện hành.

Tiền hoàn được bảo đảm bằng tiền thực theo từng đơn, không phải bằng một phiếu thu được chỉ định. Đảo thu/chi bị chặn nếu các phiếu hoàn còn hiệu lực làm âm tiền thực còn lại ở bất kỳ đơn liên quan. Đảo các phiếu hoàn phụ thuộc trước; hệ thống không tự động đảo dây chuyền. Phiếu thiếu/sai nguồn, sai tổng phân bổ hoặc sai đối tác/chiều/loại hóa đơn cần đối soát trước khi đảo.

RequestId, lý do chuẩn hóa và người thực hiện trùng khớp cho phép nhận lại kết quả cũ mà không ghi thêm. Lịch sử trước thời điểm đảo vẫn giữ tác động phiếu gốc; từ thời điểm đảo mới áp dụng bút toán ngược. Không lọc bỏ phiếu gốc khỏi mọi kỳ chỉ vì hôm nay đã bị đảo.

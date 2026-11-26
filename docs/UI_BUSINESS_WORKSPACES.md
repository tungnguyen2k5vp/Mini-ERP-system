# Business workspaces

The order, finance and reporting screens extend the established pine navigation, Be Vietnam Pro typography, white panels and warm neutral workspace.

## Layout and interaction

- Order detail puts status, outstanding quantities and links to stock documents beside the form used to receive, ship or return goods.
- The dashboard shows role-scoped metrics, pending approvals and actual warehouse movements. Finance shows invoices and their settlement available to the current role.
- Posting uses confirmation, pending controls and server validation. Narrow screens collapse the navigation and keep wide tables within labeled scroll regions. Reduced motion disables loading animation.

## Routes

| Route | Task |
| --- | --- |
| `/tong-quan` | Role scoped dashboard and operational alerts |
| `/mua-hang`, `/ban-hang` | Orders, partner filter and document creation |
| `/mua-hang/:id`, `/ban-hang/:id` | Approval, partial fulfilment, returns, reversal, timeline and print |
| `/tai-chinh` | Invoices, multi invoice payments, refunds, aging and reconciliation |
| `/bao-cao` | Dated inventory report with low/slow stock filters |
| `/nhat-ky` | Dated, searchable audit listing |
| `/tim-kiem` | Global search within authorized records |

Product listing includes actual workbook export and validated create-only import. Order/finance pages link to source order, product or stock records. Printing uses the browser's PDF destination.

Dashboard access follows `Reports.View`; users without it land on their personal profile. Stock reporting additionally requires `Inventory.View`. Dedicated order/finance options allow custom roles to complete their authorized tasks without unrelated catalog permissions. Source links appear only when the destination is authorized.

Finance shows cash settlement separately from same-order return offsets and limits allocation/refund inputs to the API's actionable amount. Payment totals sum integer cents. The allocation picker pages across invoice history while retaining selections, with the server's 100-invoice limit. Identical posting retries retain their request identifier; changed payloads receive a new one. Search opens the selected invoice within its partner/direction filter and exposes a clear-filter action.

## Validation

`npm run typecheck`, `npm run lint`, `npm run build` and the two existing Vitest checks passed. The three business Playwright tests passed together (22.9s), and the expanded final sales/visual scenario passed (17.0s) after the chart review fixes.

Chrome exercised two partial purchase receipts, a 0.1 + 0.2 multi-invoice payment (POST amount exactly 0.3), full purchase return and 0.1 refund; sales approval held two units and shipment consumed one. Dropping the successful order-create response then retrying reused the same request ID and returned the original order. An earlier partial return correctly offered no refund while unpaid invoices consumed its credit offset; the refund scenario uses a full return.

Desktop and 390px views were captured for orders, finance, stock reporting, audit, search and products; dashboard also passed 320px and 768px overflow checks. Dashboard axe found no violations after fixing account-link naming. Chart day buttons support keyboard focus, exact daily readout, date labels in UTC and a visible linear scale; complete daily data remains available below it. Initial sticky-sidebar screenshot artifacts were corrected by capturing from scroll top.

Chrome produced a tagged, one-page A4 purchase PDF. A rendered inspection checked the Vietnamese glyphs, quantities, totals and page boundaries. An independent reviewer also inspected the dashboard, order detail, mobile finance and the printed purchase. The review noted that native date inputs use the browser locale and that at 320 px the work queue appears below the metrics. The Impeccable detector returned an empty findings list. Chrome and axe covered the subsequent chart adjustments. [Purchase PDF](screenshots/purchase-print.pdf)

# Business workflows

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

# Business workflows

## Access

Sign in with a provisioned email and password. Administrators can create employees, assign roles and lock accounts. The API checks current permissions and records access changes. The personal profile shows current role grants and supports password changes.

## Master data

Create a category and unit before adding a product. Use the dedicated customer and supplier pages to record contacts and terms. Codes are unique by resource type and SKU is unique across products. Opening a record provides its current version. Saving a stale version prompts the operator to reload. Deactivation retains references from existing documents.

## Warehouse

Choose a document type and an active warehouse. Use distinct source and destination warehouses for a transfer. Adding a draft leaves stock unchanged. Post the draft to save the balance and ledger atomically. A posted issue consumes available stock. An inbound receipt updates moving-average cost and a transfer carries the source issue cost. An adjustment records a signed difference. A count records the physical count and checks the balance version captured when the draft was created.

A posted document remains in the ledger. To reverse it, open its detail and choose Đảo chứng từ. The API rejects reversal when later ledger activity exists for an affected balance. Successful reversal adds an inverse document, restores the prior average cost and links both records. Reposting an already posted document does not modify balances again.

Sales-order reservations, partial receipts linked to purchase orders, shipments linked to sales orders and financial settlement are planned in later milestones.

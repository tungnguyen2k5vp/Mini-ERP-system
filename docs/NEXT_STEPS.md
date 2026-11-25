# Next steps

1. Implement purchase order and line models with supplier, warehouse, price snapshot and approved quantities. Add Draft, PendingApproval, Approved, PartiallyReceived, Received, Completed and Cancelled transitions and enforce the Purchasing policies.
2. Link a dedicated goods receipt to a purchase order. Check remaining quantity per line when posting. Reuse StockBalance and ledger costing inside the same SQL transaction as updating the order. Test two partial receipts, duplicate posting, over-receipt and rollback before enabling the browser workflow.
3. Build sales approval and quantity reservations scoped to individual order lines, then partial issue and cancellation. Run the two-attempt reservation scenario against MiniErp_Test.
4. Add invoices, adjustments, allocations, aging and reports from the posted records. Expand seed history, Excel operations, deployment and interview material against those workflows.

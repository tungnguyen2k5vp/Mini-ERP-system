# 50 SQL Server questions grounded in MiniErp

Use [the read-only exercise script](SQL_EXERCISES_50.sql) on an authorized MiniErp development/test database. Never export authentication secrets for an interview demo.

1. **Why separate schemas?** `auth`, `catalog`, `orders`, `inventory`, `finance` and `audit` communicate ownership inside one database; they do not automatically enforce module boundaries.
2. **What is the stock balance primary key?** `(WarehouseId, ProductId)` identifies one position; it prevents duplicate balances for the same warehouse/product.
3. **Why use a rowversion?** It is an automatically changing binary concurrency token, not a business date or an ordering substitute for ledger IDs.
4. **How does the balance CHECK protect data?** `OnHand >= Reserved`, `Reserved >= 0`, `AverageCost >= 0` rejects impossible persisted balances.
5. **Why use restrictive foreign keys?** Historical documents must not disappear when catalog records are removed. Deactivation is safer than deleting referenced master data.
6. **What does a composite lookup FK prevent?** A product category FK includes kind 1 and unit FK kind 2, preventing a warehouse or wrong lookup kind from being used as either.
7. **How is SKU uniqueness enforced?** A unique index on `catalog.Products.Sku`; application prechecks improve messages but cannot replace race-safe database enforcement.
8. **Why are partner codes unique per kind?** Customer and supplier namespaces can reuse a code while each `(Kind, Code)` remains unique.
9. **Why is a request ID indexed uniquely?** It makes document retry identity durable and prevents duplicate persisted postings under concurrency.
10. **How does a nullable unique reversal index work?** SQL Server filtered indexes generated for optional unique keys allow many NULLs and at most one row per non-NULL source ID.
11. **Why decimal(19,4) for stock?** Four decimal places support fractional units and average costs. Application bounds are intentionally smaller than SQL's maximum representable value.
12. **Why decimal(19,2) for invoices?** Financial posting amounts use cents; quantity and price precision are resolved by the explicit rounding policy before persistence.
13. **What is moving average costing?** A receipt changes cost to `(old quantity * old cost + received quantity * received cost) / new quantity`, rounded to four decimals.
14. **Why persist issue cost on the movement?** Reports can use the price actually applied at posting rather than today's average or catalog purchase price.
15. **Why does reversing old movements fail?** Later stock movements make restoring an earlier cost unsafe; the service checks the latest ledger ID for every affected position.
16. **What is the stock ledger reconciliation equation?** Current OnHand equals the sum of all posted movement deltas for the position, including reversal movements.
17. **Why include original movements after reversal?** A reversal adds an opposite movement. Removing the original from a sum would double-apply the correction.
18. **What does transaction atomicity mean for fulfillment?** Stock document, movements, balances, order quantities, invoice and audit changes either all commit or all roll back.
19. **Does READ COMMITTED alone prevent a lost reservation?** No. The rowversion predicate detects the write race and forces one competing attempt to retry.
20. **What is a deadlock victim?** SQL Server aborts one transaction in a cyclic wait. The API maps error 1205 to a retriable conflict rather than pretending success.
21. **Why load stock lines in a stable order?** It reduces inconsistent lock acquisition and deadlock risk, though it cannot eliminate all deadlocks.
22. **Why not use NOLOCK for financial reports?** Dirty, missing or duplicated reads can break reconciliation. It trades correctness for apparent speed.
23. **Does one SQL statement guarantee a point-in-time report?** It avoids multiple client round trips but isolation still matters. Snapshot semantics need explicit database/transaction configuration.
24. **What is a seek-friendly date filter?** `Timestamp >= @From AND Timestamp < @To`; applying a function to the indexed column can prevent a useful seek.
25. **Why use an exclusive end date?** Half-open ranges compose across periods without guessing the timestamp precision of the final instant.
26. **Why pair timestamps with IDs in ordering?** Multiple events can share a timestamp. IDs supply a unique tie-breaker.
27. **When does offset pagination become costly?** Deep pages still scan/discard earlier rows. Keyset pagination is useful if random page-number access is unnecessary.
28. **What is keyset pagination?** Filter after the last `(timestamp, ID)` tuple and take the next bounded set using a compatible index.
29. **What makes an index useful for history?** `(WarehouseId, ProductId, Id DESC)` supports position-scoped newest-first movement reads.
30. **Why avoid speculative indexes?** Each index adds write/storage cost. Choose them using actual workload and execution-plan evidence.
31. **What is a covering index?** Its keys/included columns satisfy a query without extra table lookups; the correct choice depends on the measured query shape.
32. **How do you detect N+1 at SQL level?** Repeated similar statements per displayed row indicate avoidable round trips; use a joined/projection query where appropriate.
33. **Why preaggregate allocations before joining invoices?** Joining invoices, allocations and other detail rows directly can multiply amounts and overstate totals.
34. **When is OUTER APPLY helpful?** It computes per-invoice allocation totals while preserving invoices with no payment allocations.
35. **How are invoice balances signed?** Debit invoice outstanding is positive; a credit's outstanding is negative. Refunds allocate credits rather than subtracting twice from debit invoices.
36. **How does aging use payment history?** Include invoices issued by `@AsOf` and allocations whose payment was posted by `@AsOf`, then bucket by the due-date difference.
37. **What is the reconciliation equation?** Invoiced minus credits minus payments plus refunds equals signed outstanding allocations, at the same cutoff.
38. **Why distinguish invoice date and due date?** Invoice date determines report inclusion; due date determines overdue age. They answer different questions.
39. **Why store source stock document ID on invoices?** It gives traceability and a unique source-posting key for financial idempotency.
40. **What does a window SUM provide?** A running movement delta per warehouse/product ordered by movement ID, useful for checking every stored BalanceAfter.
41. **What does ROW_NUMBER provide?** A deterministic latest row per position when ordered by unique movement ID descending.
42. **When should HAVING be used?** After grouping, for example to return only positions where summed movements disagree with the stored stock balance.
43. **What is an anti-join useful for here?** Detect a posted fulfillment missing its invoice or a stock document missing expected source links.
44. **Why does a zero-amount invoice exist?** Free goods or cumulative fractional rounding can produce a zero financial amount with a real stock event. Zero must not imply no shipment.
45. **Why not calculate gross profit from catalog prices?** Catalog prices are current defaults. Actual revenue comes from posted invoices and cost from historical stock movements/postings.
46. **What can a CHECK constraint not guarantee alone?** Aggregate rules across rows, such as total allocations not exceeding an invoice, require coordinated transactional logic and concurrency control.
47. **Why use EF migrations instead of ad hoc DDL?** The schema change and model snapshot travel together with an ordered upgrade history; existing migration records remain intact.
48. **How do you inspect missing schema changes?** Compare the EF model with its snapshot and check pending migrations; generated SQL still needs review and safe application.
49. **How should performance exercises be run?** Use a separate test dataset, capture actual plans and logical reads, and report the measured workload. Do not claim a benchmark from a tiny demo database.
50. **What proves a backup is useful?** An authorized restore into a separate database followed by integrity and application checks. A successful backup command alone is incomplete evidence.

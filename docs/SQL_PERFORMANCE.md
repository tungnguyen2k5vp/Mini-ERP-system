# SQL Server performance

## V2 stock reconciliation on the seeded test database

A browser request waited 30,042 ms and returned 409. SQL Server DMVs showed `RESOURCE_SEMAPHORE` with `blocking_session_id=0`. The optimizer expanded the same linked-fulfillment aggregate at three references. Materializing it once into a temporary table indexed by order and product kept the comparisons in the same serializable read transaction.

The corrected query received a 5,248 KB memory grant with zero observed grant wait. Representative HTTP reads completed in 158 ms on first use and 28–44 ms on subsequent requests. Cancellation during an independent lock test released the transaction and the same physical connection could read again. These observations cover the local seeded database. The larger V2 benchmark belongs to milestone 8.

## 26 November 2026, restored development copy

Ran `database/performance/scale-restore.sql` against a fresh SQL Server Developer restore. Within one transaction it added 10,000 catalog products, 10,000 warehouse positions, 100,000 posted receipt lines and 100,000 signed ledger movements. The measured ledger contained 100,104 rows.

| Query | Reads and execution time on this run |
| --- | --- |
| Exact SKU lookup | Products 4 logical reads, 1 ms elapsed. |
| Inventory procedure, literal substring, 15-row page | Each result set read Products 249, Balances 137 and Lookups 2 pages. Total procedure elapsed 148 ms including compilation, with 125 ms CPU. |
| Net movements grouped by date across seven days | Movements 1,418 logical reads, zero physical reads, 65 ms elapsed and 63 ms CPU. Seven of the ten inserted dates matched. |

The exact key seek stays selective; the substring-filtered count and page each scan matching candidates. The period query aggregated most of the synthetic history, for which a scan was expected. These are single local observations with warm pages, not latency percentiles. The script validated original counts after rollback (109 products, 97 balances, 104 movements). SQL identity sequences can advance on a rolled-back insert, hence the required disposable restore prefix.

## Original catalog checkpoint

Ran database/performance/catalog-and-inventory.sql on MiniErp with SQLCMD, using STATISTICS IO, STATISTICS TIME and SHOWPLAN_TEXT.

| Query | Observed logical reads | Observed plan or shape |
| --- | --- | --- |
| Product detail by exact demo SKU | Products 4 | Seek on IX_Products_Sku followed by key lookup on PK_Products for Name. |
| Inventory report filtered to VP-01, page of 15 | Per result set, Products 192, Balances 7, Lookups 2 | Count and paged results each join existing balance rows to products and warehouses. |

The inventory sample had 96 opening positions before browser transfers. These are measurements on the demo and SQL Server 2022 Developer instance. CHARINDEX treats the search as a literal substring and cannot use an ordinary index seek for that predicate. The order by SKU and unique keys gives deterministic OFFSET/FETCH on unchanged data. Review read amplification on a larger dataset before expanding the indexes.

The movement index by warehouse, product and descending ID supports reversal checks. The unique request and movement indexes protect write invariants. The scripts in database/scripts list the applied columns, constraints and indexes.

~~~powershell
sqlcmd -S lpc:localhost -E -C -d MiniErp -b -i database/performance/catalog-and-inventory.sql
~~~

# SQL Server performance

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

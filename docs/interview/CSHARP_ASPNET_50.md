# 50 C# / ASP.NET Core questions grounded in MiniErp

These are discussion prompts with answer outlines, not claims that every production concern is solved. Start in `backend/src`; run verification using the repository scripts and the isolated test database.

1. **Why use a modular monolith here?** Orders, inventory, finance and identity share one deployable process and SQL transaction while retaining feature namespaces. Distributed transactions would add complexity without a demonstrated need.
2. **Where do business invariants live?** `StockBalance` protects available stock; `TradeOrder` protects workflow transitions. Application contracts describe transport-independent inputs; infrastructure services orchestrate persistence.
3. **Why is `decimal` used?** Quantities/costs use four decimal places, financial amounts two. Binary floating point cannot represent many ordinary money values exactly.
4. **What rounding policy does the project use?** `Money.Round` uses two decimals and `MidpointRounding.AwayFromZero`; stock average cost uses four decimals with the same midpoint policy.
5. **How do partial shipments avoid losing a cent?** The charge is `Round(cumulativeAfter * price) - Round(cumulativeBefore * price)`, rather than independently rounding every partial quantity.
6. **What does `init` protect?** It restricts normal C# assignment after construction, useful for historical stock lines and finance sources. Database constraints and API workflows remain necessary.
7. **Why use records for API contracts?** Positional records give concise immutable value-shaped DTOs. They are distinct from tracked EF entities.
8. **How are document states protected?** `Submit` requires Draft; `Approve` requires PendingApproval; fulfillment checks approved workflow state and remaining quantities.
9. **What is optimistic concurrency?** SQL Server `rowversion` becomes an EF concurrency token; the update matches the previously read version, and a competing change produces `DbUpdateConcurrencyException`.
10. **Why also send an order version in requests?** It detects a user acting on stale displayed state before changing the workflow. EF still protects the database race after that comparison.
11. **What does base64 mean for a rowversion?** It is a transport encoding for opaque bytes, not a timestamp, password or authorization token.
12. **What makes sales approval atomic?** All line reservations, order state and audit writes use the same context and SQL transaction. Any failed line rolls everything back.
13. **Can a transaction alone prevent overselling?** No. The balance rowversion detects competing updates; the domain checks available stock and SQL constraints enforce valid balances.
14. **How is posting retried safely?** A stable request GUID plus a unique index identifies a fulfillment. A repeated payload returns existing data; a different payload using the same key conflicts.
15. **What happens to simultaneous identical requests?** Database uniqueness/concurrency can reject one racing attempt. Retrying after the successful commit resolves to the existing posting.
16. **Why compare the retry payload?** Accepting the same key with different quantities could silently report a different operation as successful.
17. **Why not put `SaveChangesAsync` in the finance hook?** `IFinancePosting` stages invoice entities into the shared context so the caller owns commit/rollback across modules.
18. **Why are some intermediate saves necessary?** Database-generated stock document IDs are required for source links. An enclosing transaction prevents those saves from becoming partial commits.
19. **Why use `await using` on transactions?** It asynchronously disposes resources and rolls back an uncommitted transaction when an exception exits the scope.
20. **What does a cancellation token do here?** It propagates request cancellation to EF, Dapper and stream I/O. It does not replace transaction rollback or domain validation.
21. **Why is a scoped context registered?** A request gets a unit of work. A singleton context would share mutable tracking state across concurrent requests.
22. **Can two EF queries run with `Task.WhenAll` on the same context?** No. EF contexts do not support overlapping operations; await sequentially or use separate contexts.
23. **When is `AsNoTracking` useful?** Read-only entity queries avoid tracking overhead. It is unnecessary when a projection contains only scalar DTO values.
24. **How does the project avoid N+1?** Services project scalar DTOs or include needed navigation data explicitly rather than enabling lazy loading.
25. **Why use Dapper for reports?** Parameterized SQL expresses aging, running totals and reconciliation clearly while EF owns tracked writes and migrations.
26. **What is a safe Dapper parameter?** `CommandDefinition(sql, new { At = at }, cancellationToken: ct)` binds values. User input must not be concatenated into SQL identifiers or clauses.
27. **How are sortable columns kept safe?** Endpoints accept an allowlist and services choose fixed expressions. An arbitrary client SQL column is never interpolated.
28. **Why is ordering unique before pagination?** A tie-breaker such as `Id` makes each page deterministic for a fixed dataset. Concurrent inserts can still shift offset pages.
29. **What does dependency injection buy here?** The clock, context and posting service are explicit dependencies. One scoped finance service implements both the API service and posting interface.
30. **Why inject `TimeProvider`?** It centralizes the clock and allows deterministic test clocks without hiding time reads in business code.
31. **How does authentication differ from authorization?** JWT validation identifies an active session; permission policies determine whether that identity can act on a module/resource.
32. **Why check order kind before authorization?** Purchase and sales documents share storage but require different permission families. A sales grant must not expose purchasing records.
33. **Why are frontend permission checks insufficient?** They guide the UI; callers can bypass the browser. The API rechecks permissions for each operation.
34. **How do administrator grants evolve?** `ProfileReader` returns the current `Permissions.All` for administrators, so existing administrators receive newly declared capabilities without rewriting role claims.
35. **Why check sessions on JWT validation?** It allows revoked sessions to stop working before the access token expires; the database check has a performance cost.
36. **Where is the refresh credential stored?** The browser receives a cookie; persistence retains its hash. Raw refresh credentials should not appear in logs or source control.
37. **Why validate request origin for cookie operations?** It helps defend credentialed browser session actions against cross-site requests; it complements cookie settings and endpoint design.
38. **What does Problem Details provide?** A consistent HTTP error envelope with code and correlation ID, while keeping internal exception details away from the client.
39. **Which errors are conflicts?** Stale rowversions, duplicate document requests and stock/workflow conflicts return 409. Invalid inputs return 400 and missing resources 404.
40. **Why avoid logging exception messages indiscriminately?** Connection strings, credentials and user data can leak through exception details. `ApiErrors` logs bounded diagnostic context.
41. **Why are finance records append-only?** Corrections use credits/refunds. EF after-save guards help prevent accidental mutation; this is not a complete database-level immutable ledger.
42. **Why block direct reversal of linked stock documents?** Reversal must also update order quantities and finance. The order entry point coordinates all three atomically.
43. **Why require latest-movement reversal?** Restoring a previous moving average is only safe when later movements have not consumed or changed the position. Otherwise use a new correction document.
44. **What is the difference between return and reversal?** A return records a new commercial/stock event; reversal cancels the effect of a specific source posting under stricter history rules.
45. **How is stock cost captured?** Issues use the current average cost and persist it on movements; subsequent catalog price changes do not rewrite historical cost.
46. **What validates an XLSX import?** `ProductWorkbook` bounds ZIP/XML size, rejects formulas/external content, validates cell references and values, resolves active codes, and commits the whole batch.
47. **Why not trust a workbook filename?** A `.xlsx` suffix does not prove valid OOXML. The archive and XML content must be parsed and validated.
48. **Why are SQL integration tests needed beyond unit tests?** Unit tests cannot reproduce rowversion, SQL constraints, unique indexes, real transaction rollback or EF SQL translation.
49. **Why use normal API entry points in integration tests?** They exercise authentication, authorization, binding, orchestration and persistence together rather than bypassing transport/business boundaries.
50. **What evidence is needed before calling the app production-ready?** Verified builds, isolated SQL/API/browser flows, deployment, backup/restore, concurrency, observability and workload-specific operational checks. Unverified checks must remain explicit.

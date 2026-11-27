# Code review checkpoint

## V2 milestone 2 · 27 November 2026

| Finding | Correction and verification |
| --- | --- |
| A create retry compared its payload to mutable draft lines. | Serialize concurrent creates on the request ID and compare an immutable SHA-256 projection. Preserve decimal equivalence, line order independence and the original command on the first edit of a migrated draft. |
| Replaying a stock post could reject the original version after the first post succeeded. | Store the submitted token and the actor with the successful audit event. An exact retry returns the existing result. An edited draft requires a version. SQL regression checks one movement and one post event. |
| EF could retain accepted intermediate changes after the encompassing transaction rolled back. Separate detail queries could combine two revisions. | Outer commands clear tracked state when they end. Hold the document header lock through the caller's transaction, including a READ COMMITTED create retry. A SQL regression holds the reader open, observes a writer lock timeout, then releases the reader and verifies exactly one edit. A separate HTTP race checks the complete winning draft and its author. |
| A stale editor could discard local work or apply it against a silently refreshed version. Restricted stock editors could send a catalog price for a replacement SKU. | Keep the loaded draft and version together. Discard-and-reload requires confirmation. Warehouse users send null cost, and the server resolves the price by SKU. Verified with two live browser tabs and a warehouse login. |
| A failed stock detail refetch could unmount an open editor and discard its unsaved fields. | Render the editor from its loaded draft when cached data exists. Chromium, Firefox and WebKit each interrupted a background GET, retained the note and saved it using the original rowversion. |
| The API captured the approval threshold before the host applied its final configuration. | Resolve the policy from the built host and validate it on startup. An HTTP test configures a threshold, exercises its exact boundary and verifies an independent approval below it. |

The server checks approver and amount grants under the document workflow. The audit-event timeline omits price snapshots. Full historical draft restoration remains outside this delivery. [Permission matrix](API_PERMISSION_MATRIX.md) · [Test evidence](TEST_REPORT.md)

## V2 milestone 1

| Finding | Correction and regression |
| --- | --- |
| Standalone reversal checked fulfillment ownership before loading the committed stock document. | Load the source in the reversal transaction before checking its link. Verify linked documents reject direct reversal and order reversal preserves source cents. Cancellation retries produce one audit entry. |
| Aggregate debt comparisons could hide a missing invoice, invalid cash allocation or mispriced line. | Compare invoice, order, fulfillment, stock and allocation facts by source. Rollback-only fixtures introduce missing and mismatched records. Incomplete historical cost attribution is excluded from the cost check. |
| Opposite reservation errors on two order lines could produce the correct warehouse total. | Compare stored line states and quantities with net linked postings independently before aggregating by warehouse and SKU. |
| Rounding each shipment and credit separately could settle SKU A's remaining cost on SKU B. | Save per-line cents and allocate remaining cost per SKU. Exact reversals copy the source snapshot. The historical document-only path remains available. |
| A multi-statement finance read could mix commits. Serializable readers also competed with a writer through different indexes. | Use a single read transaction. Retry an entire pure invoice/payment page only for SQL Server deadlock 1205, with a bounded attempt count. An orchestrated reader/writer regression accepts either consistent serial ordering. |
| Cost fields reached ordinary catalog/warehouse roles, including through workbook export. Purchase sorting could also disclose the ranking of restricted amounts. | Apply current server-side financial field rules to JSON, sort and workbook responses. Regression tests exercise six ordinary roles, permission revocation with a live token, warehouse posting and printed detail. |
| Dashboard, audit and finance used different end-of-day semantics. Widened SQL SUM expressions could drop cents on subtraction. | Explicit Vietnam-day ranges, datetime2 parameters and fixed-scale intermediate arithmetic. SQL regression checks year rollover, the last 100 ns, signed one-cent debt and split-line attribution. |

## Upgrade review

- A clean online npm install exposed missing entries that an offline dry run hid. Regenerating from pinned dependencies and verifying real installation closed this gap.
- A competing payment generated SQL deadlock 1205 wrapped by EF as InvalidOperationException. Map nested SQL transaction conflicts to 409. Serialize related payments, order transitions and returns with the same ordered SQL locks.
- Unpaid merchandise returns could previously leave both an overpayable debit and a refundable credit. Same-order offsets now reduce the actionable amount and refunds are capped by actual historical cash.
- A sale, return and later shipment at a new cost exposed an incorrect average across already returned units. Use net outstanding signed movements and settle the final document-level cost cents. Integration tests exercise a subsequent reversal and re-return.
- SQL Server datetime2 loses the .NET Kind. JSON responses explicitly serialize operational timestamps as UTC. One read-only session check retries after an aborted pooled SQL batch. Mutating business commands retain explicit transactions and request identities.
- Independent screen review found chart labels difficult to read beside the opening-stock outlier. The daily chart now has a scale, keyboard-accessible exact values and an equivalent data table.

| Area | Finding | Resolution or status |
| --- | --- | --- |
| Architecture and deployment | Kestrel held file locks on MSBuild output. | Start from a staged copy in .work/api-run. The stop script verifies the process entry point. Subsequent builds succeeded with the API running. |
| C# and UX | Background refetch could update the version displayed by an open catalog editor. | Capture the loaded version at editor entry. The API and SQL then reject an edit based on an obsolete version. |
| SQL integrity | Duplicate documents or conflicting balances can corrupt stock. | Unique request and ledger keys, SQL CHECK constraints, transactional writes and rowversion. Tests cover a competing issue and multi-line rollback. |
| Business rules | Reversal after a newer transaction could change the average cost retrospectively. | Restrict reversal to the most recent movement on each affected balance, then restore the prior average via an inverse document. |
| Security | The initial NuGet restore flagged transitively resolved Microsoft.OpenApi and PKCS packages. | Pin patched versions and regenerate lockfiles. See the tested build result. Browser cookies and server authorization were exercised. |
| UI and accessibility | The initial Playwright selector also matched the list filter during the lazy route transition. | Wait for the stock form heading and use an exact field label. The form uses the selected stock kind for its layout, validation and request. |
| Test environment | Docker Desktop engine was stopped. | SQL integration runs against MiniErp_Test on the existing SQL Server. Docker and CI remain pending. |
| Dependency setup | Newer form resolver metadata added an optional AJV 8 peer that conflicted with the ESLint AJV 6 tree in npm ci. | Pin the Zod adapter to 5.2.2 and regenerate the lockfile. |

See [test results](TEST_REPORT.md), [known issues](KNOWN_ISSUES.md) and [SQL measurements](SQL_PERFORMANCE.md).

# Code review checkpoint

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

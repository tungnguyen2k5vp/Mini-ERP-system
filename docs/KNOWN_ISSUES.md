# Known issues

- The Codex sandbox identity cannot authenticate to local SQL Server. SQL verification runs with the Windows account via the managed tool permission.
- The Docker daemon was not running during environment discovery.
- The active Codex conversation's skill list was loaded before .NET skills were copied. See the inventory for verification by path and discovery status.
- Standalone warehouse documents are implemented. Sales-order reservations, order-linked postings and finance remain pending.
- Browser tests verify responsive layout and keyboard interaction in Chrome. Screen reader and other browser coverage remain unverified.
- Vite reports two harmless annotation warnings from Zod's generated source when bundling with Rollup. The build produces the frontend successfully.
- ESLint 9 is deprecated upstream. The project pins it for the current toolchain. A maintained ESLint upgrade and broader dependency review belong to the CI milestone.
- The final npm lockfile passed dry-run validation. Clean installation and frontend tests with that exact dependency tree remain unverified. See the test report for the approval and cache results.
- Rapid page navigation can cancel pending reads. SqlClient sometimes logs the server-side cancellation as an error even though the browser has already dropped the request.

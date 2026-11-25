# Local deployment

Install a supported .NET 10 SDK and Node.js 22.12+ or 24, and make a dedicated SQL Server development instance available. The Windows scripts currently use the existing SQL Server default instance over the local shared-memory protocol and Windows Authentication.

Run from the MiniErp root.

~~~powershell
.\scripts\setup.ps1
.\scripts\migrate-db.ps1
.\scripts\seed-demo.ps1
.\scripts\start-dev.ps1
~~~

The startup script launches the Vite frontend and API on loopback. Inspect .work/api.log and .work/frontend.log for process diagnostics. Stop the recorded processes with scripts/stop-dev.ps1. Startup retains the development database and demo credentials.

For an alternate SQL Server instance, set ConnectionStrings__Erp, Auth__SigningKey and Auth__PublicOrigin, then run the built API directly with --migrate. For first production provisioning use --provision-admin and supply Seed__AdminEmail and Seed__AdminPassword via protected environment configuration. Use the HTTPS public frontend origin and proxy /api to the private API. The runtime processes should use a dedicated SQL account with permissions for their actual queries. Use a migration account to deploy schema changes.

# MiniErp

Inherit any workspace-level AGENTS.md and AGENTS.override.md. Restrict project edits to this directory. The shared skills live in D:/ReactJS/.agents/skills and the vetted upstream .NET skills are in D:/ReactJS/.agents/skills/dotnet. Do not use skills-quarantine. Open the relevant SKILL.md by path if the current session does not list it. Review a skill's referenced commands before running them.

At the start of a continuation, read docs/PROJECT_STATUS.md, docs/TASKS.md, docs/NEXT_STEPS.md and docs/DECISIONS.md. Apply dotnet/csharp-expert for routing, dotnet/dotnet-webapi for endpoints, dotnet/create-datadriven-aspnetcore and dotnet/optimizing-ef-core-queries for EF work, dotnet/msbuild and dotnet/directory-build-organization for project files, and dotnet/run-tests for .NET checks. Apply Impeccable in product mode for UI and inspect real desktop and mobile renders.

Use English identifiers, Vietnamese UI copy, UTC timestamps, decimal quantities/money and cancellation-aware async I/O. Keep business rules in Domain/Application and keep persistence in Infrastructure. API owns transport, authentication and permission policies. Frontend routes are feature-oriented and use the real API.

EF Core migrations own SQL Server schema changes. Use explicit constraints, restrictive foreign keys for history and rowversion for concurrent edits. Posting, audit and ledger writes belong in the same transaction. Use stable document state or a unique request identifier for retried posting. Use parameterized Dapper commands for reports.

Keep secrets in configuration outside source control. Use server-side authorization, validation and Problem Details. Use only separate MiniErp development and test databases. Preserve migrations and existing records.

For a completed slice, run backend build, relevant SQL Server integration tests and frontend build/tests. Verify affected flows through the normal entry point. Document the feature and actual results, marking environment-dependent checks that remain unverified.

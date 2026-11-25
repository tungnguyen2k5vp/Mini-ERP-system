# Architecture decisions

## 1. Modular monolith

The core procurement, sales, inventory and finance invariants involve the same source records. An in-process application and one SQL Server instance support straightforward EF Core transactions, deployment and SQL investigation. Boundary crossing uses a use-case contract where it removes a persistence dependency.

## 2. Session and authorization

A short-lived JWT refers to a SQL-backed session. Identity owns passwords and lockout. A session row contains the hash of the current refresh credential. A rowversion arbitrates concurrent refreshes. Each protected request checks whether the current session and account are active. Permission policies read current grants, so an administrator can tighten a role without waiting for JWT expiry.

## 3. Protected cookie operations

The frontend proxies requests on the same origin during development. Cookie operations require the configured Origin and a custom header. Production uses HTTPS and a Secure, HttpOnly, SameSite=Strict cookie.

See [decisions](../DECISIONS.md) for environment and implementation choices.

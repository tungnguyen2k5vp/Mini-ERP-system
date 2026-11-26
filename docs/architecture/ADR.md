# Architecture decisions

## 1. Modular monolith

The core procurement, sales, inventory and finance invariants involve the same source records. An in-process application and one SQL Server instance support straightforward EF Core transactions, deployment and SQL investigation. Boundary crossing uses a use-case contract where it removes a persistence dependency.

## 2. Session and authorization

A short-lived JWT refers to a SQL-backed session. Identity owns passwords and lockout. A session row contains the hash of the current refresh credential. A rowversion arbitrates concurrent refreshes. Each protected request checks whether the current session and account are active. Permission policies read current grants, so an administrator can tighten a role without waiting for JWT expiry.

## 3. Protected cookie operations

The frontend proxies requests on the same origin during development. Cookie operations require the configured Origin and a custom header. Production uses HTTPS and a Secure, HttpOnly, SameSite=Strict cookie.

See [decisions](../DECISIONS.md) for environment and implementation choices.

## 4. Partial fulfillment and recorded cost

Keep the approved order separate from stock and finance events. Each fulfillment posts the actual quantity. A sale first releases its own reservation and then issues stock. The invoice records this event's agreed value and ledger cost. Monetary deltas use cumulative rounding so splitting a line across shipments cannot change its total by a cent.

## 5. Credits and cash allocation

Match unpaid credits to unpaid debits within the same order before accepting cash allocations. The settlement calculation determines collectible amounts and caps refunds by net cash collected. Payments acquire the affected order locks in sorted order, validate the complete allocation and commit once. This preserves independent order histories when one transfer settles multiple invoices.

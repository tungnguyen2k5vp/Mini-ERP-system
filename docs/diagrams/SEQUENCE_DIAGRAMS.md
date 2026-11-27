# Session sequences

~~~mermaid
sequenceDiagram
    participant B as Browser
    participant A as API
    participant S as SQL Server
    B->>A: POST /auth/login and origin header
    A->>S: Validate Identity password and lockout
    A->>S: Insert session hash and audit event
    A-->>B: Short-lived JWT and HttpOnly cookie
    B->>A: POST /auth/refresh with session cookie
    A->>S: Compare hash, status and rowversion
    A-->>B: New JWT and rotated cookie
    B->>A: GET /users with bearer token
    A->>S: Check active session and current role grants
    A-->>B: Page or 403
~~~

## Stock posting

~~~mermaid
sequenceDiagram
    participant U as Warehouse employee
    participant A as StockService
    participant D as SQL Server
    U->>A: Post draft with original rowversion
    A->>D: Begin transaction and load document and balances
    alt Exact successful token and actor replay
        A-->>U: Existing posted document, no new writes
    else New posting intent
        A->>A: Compare rowversion, state, availability and cost
    end
    A->>D: Save balance, document, ledger, audit with rowversion
    alt All constraints and versions match
        D-->>A: Commit
        A-->>U: Posted document
    else Concurrent change or failed rule
        D-->>A: Rollback
        A-->>U: Problem Details
    end
~~~

## Fulfillment and payment

~~~mermaid
sequenceDiagram
    participant O as Operator
    participant C as OrderService
    participant I as StockService
    participant F as FinanceService
    participant D as SQL Server
    O->>C: Post partial shipment with request ID
    C->>D: Begin transaction and lock order
    C->>D: Check request, remaining units and reservation
    C->>I: Issue actual units in shared transaction
    I->>D: Update balance and record ledger cost
    C->>D: Attribute posted sales cost by SKU if order history permits
    C->>F: Stage invoice for posted stock document
    F->>D: Record source, amount, due date and cost
    C->>D: Save fulfillment and audit, commit
    O->>F: Allocate payment to invoices
    F->>D: Lock source orders in ID order
    F->>D: Check credits, prior cash and outstanding balances
    F->>D: Commit payment, allocations and audit
~~~

## Draft edit and approval

~~~mermaid
sequenceDiagram
    participant U as Maker
    participant A as Order API
    participant D as SQL Server
    participant P as Approver
    U->>A: PUT draft with version, lines and expectedDate
    A->>D: Begin isolated write, lock order, compare version
    A->>D: Freeze legacy create hash; replace lines and append edit audit
    A->>D: Commit draft and clear tracked state
    U->>A: Submit with version
    A->>D: Lock, compare version, append submit audit and commit
    P->>A: Approve with version
    A->>D: Lock order and read maker audit IDs
    A->>A: Require module grant, maker separation and inclusive threshold override
    alt Policy and stock permit approval
        A->>D: Reserve sales balances, update state, append audit, commit
        A-->>P: Coherent detail and history
    else Stale version, policy or stock failure
        A->>D: Rollback and discard tracked changes
        A-->>P: Problem Details
    end
~~~

Reject and return-for-revision require an approver grant and a reason. Returned orders require a real edit before resubmission; rejection is terminal. Cancellation requires its grant and a reason, preserving existing fulfillment and invoices. Exact latest transition retries match action, original version, actor and reason. History provides event metadata rather than prior draft contents.

Stock edits use the same version boundary with immutable kind. Count edits require explicit recount confirmation. Hidden retained-product costs are preserved; new restricted receipt products use server-owned purchase-price snapshots. Create retries serialize on request ID and compare the original normalized payload hash.

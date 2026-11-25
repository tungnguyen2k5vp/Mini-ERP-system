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
    U->>A: Post draft
    A->>D: Begin transaction and load document and balances
    A->>A: Validate state, availability and cost
    A->>D: Save balance, document, ledger, audit with rowversion
    alt All constraints and versions match
        D-->>A: Commit
        A-->>U: Posted document
    else Concurrent change or failed rule
        D-->>A: Rollback
        A-->>U: Problem Details
    end
~~~

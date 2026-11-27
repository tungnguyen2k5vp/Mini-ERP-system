# Entity relationships

~~~mermaid
erDiagram
    PAYMENT_REVERSALS {
        bigint Id PK
        bigint PaymentId FK,UK
        uniqueidentifier RequestId UK
        datetime2 PostedAtUtc
        string Reason "max 300"
        uniqueidentifier UserId "nullable"
    }
    ORDERS {
        int Id PK
        byte Status "1-8; includes rejected and returned for revision"
        date ExpectedDate "nullable"
        varchar CreatePayloadHash "nullable SHA-256"
        rowversion Version
    }
    DOCUMENTS {
        int Id PK
        varchar CreatePayloadHash "nullable SHA-256"
        bit IsEdited
        rowversion Version
    }
    AUDIT_LOGS {
        bigint Id PK
        uniqueidentifier UserId "nullable historical actor"
        string Entity
        string EntityId
        string Action
        datetime2 TimestampUtc
        string Reason "nullable; max 300"
        string InputVersion "nullable; base64 rowversion"
    }
    DOCUMENT_LINES {
        int DocumentId PK,FK
        uniqueidentifier ProductId PK,FK
        decimal Quantity
        decimal UnitCost
        decimal PostedSalesCost "nullable attributed sales cost"
    }
    USERS ||--o{ USER_ROLES : assigned
    ROLES ||--o{ USER_ROLES : grants
    ROLES ||--o{ ROLE_CLAIMS : contains
    USERS ||--o{ REFRESH_SESSIONS : owns
    USERS |o..o{ AUDIT_LOGS : attributed_to
    LOOKUPS ||--o{ PRODUCTS : category_or_unit
    LOOKUPS ||--o{ BALANCES : warehouse
    PRODUCTS ||--o{ BALANCES : held_as
    LOOKUPS ||--o{ DOCUMENTS : origin_or_destination
    DOCUMENTS ||--|{ DOCUMENT_LINES : specifies
    PRODUCTS ||--o{ DOCUMENT_LINES : recorded_item
    DOCUMENTS ||--o{ MOVEMENTS : posts
    PRODUCTS ||--o{ MOVEMENTS : moved_item
    LOOKUPS ||--o{ MOVEMENTS : moved_at
    DOCUMENTS |o--o| DOCUMENTS : reverses
    BUSINESS_PARTNERS ||--o{ ORDERS : trades
    ORDERS ||--|{ ORDER_LINES : contains
    PRODUCTS ||--o{ ORDER_LINES : snapshots
    ORDERS ||--o{ FULFILLMENTS : permits
    DOCUMENTS ||--o| FULFILLMENTS : posts
    DOCUMENTS ||--o| INVOICES : generates
    ORDERS ||--o{ INVOICES : originates
    INVOICES ||--o{ ALLOCATIONS : settles
    PAYMENTS ||--|{ ALLOCATIONS : distributes
    PAYMENTS ||--o| PAYMENT_REVERSALS : corrected_by
    BUSINESS_PARTNERS ||--o{ PAYMENTS : pays_or_receives
~~~

Audit attribution retains the user identifier in the historical record. SQL Server does not enforce a cascading dependency from audit entries.

Successful audit events supply maker attribution and document history through Entity/EntityId, with an Entity/EntityId/Id index. No historical creator is inferred when attribution is absent. Create hashes identify original input across edits; old draft contents are not retained for restoration.

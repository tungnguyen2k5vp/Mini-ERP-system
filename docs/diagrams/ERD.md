# Entity relationships

~~~mermaid
erDiagram
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
~~~

Audit attribution retains the user identifier in the historical record. SQL Server does not enforce a cascading dependency from audit entries.

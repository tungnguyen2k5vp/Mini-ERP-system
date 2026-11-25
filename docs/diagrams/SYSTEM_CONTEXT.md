# System context

~~~mermaid
flowchart LR
    Employee["Employee or administrator"] -->|Browser| UI["MiniErp React application"]
    UI -->|HTTPS, REST| API["MiniErp ASP.NET Core API"]
    API -->|SQL Server connection| DB["MiniErp database"]
~~~

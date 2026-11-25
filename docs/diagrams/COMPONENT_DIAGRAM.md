# Components

~~~mermaid
flowchart LR
    Web["React app"] --> API["API"]
    API --> App["Application"]
    API --> Infra["Infrastructure"]
    Infra --> App
    App --> Domain
    Infra --> SQL["SQL Server"]
~~~

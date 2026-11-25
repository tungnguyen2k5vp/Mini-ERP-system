# Implementation plan

Begin with the Identity session, role policies and a real SQL Server migration. Add catalog and partner records before stock. Connect stock to purchasing (partial receipt) and sales (reservation and shipment) before recording invoices and allocations. Implement reports over the posted records and complete the portfolio material against the resulting source.

Each slice adds the needed domain rules, an EF migration where the model changes, versioned API routes, React screens and tests at the real entry point. See [tasks](TASKS.md) for progress.

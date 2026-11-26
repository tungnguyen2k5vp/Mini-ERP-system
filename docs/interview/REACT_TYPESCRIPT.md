# React / TypeScript interview prompts

Discuss the real implementation under `frontend/src`, especially feature modules, `lib/api.ts`, the query layer, shared feedback components and permission-aware navigation.

1. **Why use feature-oriented routes?** Orders, finance, catalog and inventory own their screens and API contracts; the shell supplies shared navigation and layout.
2. **Why is a TypeScript type not API validation?** Types disappear at runtime. The server validates every trust boundary and remains authoritative.
3. **Where should the access token live?** The app holds it in memory; the refresh credential is a server-controlled cookie. A reload restores a session through the real API.
4. **Why centralize fetch behavior?** Shared handling keeps authorization headers, errors and session recovery consistent across feature screens.
5. **What makes a query key correct?** It includes all filters and identifiers that change the result, preventing one status/page/partner from reusing another result.
6. **When should a mutation invalidate data?** After successful posting, invalidate the affected order/list/balance/finance/report queries so the next view reflects committed server state.
7. **How do you prevent double-click posting?** Disable the action while pending and reuse the operation's request ID on retry. The server unique key remains the final guard.
8. **How are stale updates presented?** Preserve user context, show the conflict, reload current data and require a fresh action against its version.
9. **Why debounce search?** It reduces unnecessary requests while typing. Pagination must reset when the effective filter changes.
10. **Why distinguish loading, empty and error states?** They have different recovery actions; a failed query must not appear as an empty, successful list.
11. **Why preserve previous page data carefully?** It can reduce flicker, but the UI must indicate pending replacement and avoid applying actions to stale selection.
12. **How should decimal fields be handled?** Validate input syntax/ranges and display Vietnamese formatting; the API performs decimal arithmetic and final rounding.
13. **Why not calculate inventory availability only in React?** The browser cannot see competing transactions. It can display OnHand minus Reserved, but the server controls reservation/posting.
14. **How do scoped order options improve authorization?** `/orders/options?kind=` can authorize the order feature directly, without assuming unrelated catalog/inventory view grants.
15. **Are hidden buttons a security boundary?** No. Every API operation checks the matching permission and the resource's purchase/sales kind.
16. **What makes a table accessible?** Semantic headers, meaningful labels, visible keyboard focus, usable action names and controls that remain operable at small widths.
17. **How do modal forms stay accessible?** Manage initial/return focus, label fields, expose errors and preserve keyboard dismissal when safe.
18. **What should an error toast contain?** A concise actionable message; detailed field errors belong with the input. Avoid leaking internal stack traces.
19. **How should an XLSX upload report failure?** Display the server's row-specific error and retain a clear retry path. An invalid batch must not appear partially successful.
20. **Why revoke object URLs after downloads?** Temporary blob URLs retain resources until released; a shared download helper can manage that lifecycle.
21. **What does a responsive ERP layout prioritize?** Readable values, reachable filters/actions and deliberate table overflow rather than shrinking every column until unusable.
22. **What belongs in component state versus server state?** Temporary form/selection state stays local; persisted lists/details belong in the query cache with explicit invalidation.
23. **What does an end-to-end test add?** It verifies navigation, permission-conditioned controls, actual HTTP behavior and rendered desktop/mobile interactions together.
24. **Why not use mock statistics in a portfolio dashboard?** A meaningful dashboard reconciles to real posted source documents and states its date/filter scope.
25. **What evidence supports UI completion?** A successful frontend build/test run plus actual desktop/mobile browser inspection of the changed flows. Screenshots alone do not prove posting correctness.

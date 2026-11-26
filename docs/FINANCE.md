# Operational finance

Each posted order fulfillment, return or reversal creates one immutable internal invoice/credit linked to its order and stock document. Amounts use two decimal places, rounding midpoint away from zero; partial fulfillment/return totals use cumulative line deltas so full quantities reconcile to their order price snapshots. Sales cost comes from the posted stock ledger. This module does not implement statutory accounting or official electronic invoices.

## Settlement and returns

Payments must allocate their entire positive amount to one or more invoices for the same partner and receivable/payable direction. A request identifier is mandatory; retrying the same normalized payload returns the original payment, while reusing its identifier with a different payload conflicts. Refunds allocate credit invoices and require actual cash previously paid against their source order.

Unpaid returns automatically offset the oldest unpaid invoices **within the same order and direction**. The offset is derived from immutable source documents and cash allocations; it does not create a cash payment or rewrite invoice amounts. The invoice API exposes:

| Field | Meaning |
| --- | --- |
| `settled` | Actual cash allocated to this invoice/credit. |
| `offsetAmount` | Same-order debit/credit offset, allocated by ascending invoice ID. |
| `outstanding` | `amount - settled - offsetAmount`; signed credit balances subtract in debt reports. |
| `actionableAmount` | Maximum cash allocation/refund currently available on this invoice. Refunds are additionally limited to actual net cash already received/paid for the source order. |

For debit 100 and return credit 40 before cash, only 60 is collectible and no refund is available. Paying that 60 closes both source documents. Returning all 100 before payment closes both without a receipt or refund. If a later correction makes net invoice value negative, the extra credit remains visible but cannot create a cash refund larger than cash actually received.

Payment, return and reversal transactions serialize on their source order rows using `UPDLOCK,HOLDLOCK`. Multi-order payments acquire those locks in ascending order ID, validate every allocation, then save payment/allocations/audit together. Conflicting requests roll back entirely. SQL deadlock/timeout conflicts are retriable; reuse the same request identifier after an ambiguous response.

The aging report applies the same credit offsets using only invoices and cash allocations present at its historical UTC cutoff. Reconciliation independently compares signed invoice/credit totals, payment/refund totals and signed outstanding balances. No balances or revenue are inferred from draft orders.

## Verification

`SettlementTests` covers unpaid partial/full returns, negative net invoice values, cash-backed refunds and multiple source documents. `FinanceTests` covers the API, semantic retries, overpayment, net settlement, refunds before cash, competing same-invoice/multi-order allocations, concurrent return/payment, permissions and reports. `ReportingTests` covers actual purchasing/sales/return source reconciliation and restricted dashboard/search visibility.

The tests passed against the isolated SQL Server databases on Windows and Ubuntu. [Test report](TEST_REPORT.md)

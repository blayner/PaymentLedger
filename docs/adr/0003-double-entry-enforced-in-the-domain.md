# ADR-0003: Double-entry bookkeeping enforced in the domain model

- Status: Accepted
- Date: 2026-04-20

## Context

Single-entry balance updates ("add 100 to Alice") make it easy for money to appear or disappear:
a deposit that credits the customer but never records where the cash came from leaves the books
unbalanced, and nothing notices until reconciliation.

## Decision

- Every balance change is one side of a `Transaction` made of `LedgerEntry` items.
- `Transaction.Create` refuses to build a transaction unless:
  debits equal credits, there are at least two entries, all entries share one currency,
  and no account appears twice.
- Direction is expressed by `Debit`/`Credit`, never by sign: entry amounts are always positive.
- Account types follow the platform's perspective: customer wallets are **liabilities**
  (credit increases them), settlement accounts are **assets** (debit increases them).
- `PostingRules` encodes the chart of accounts per operation. `LedgerPostingService` validates every
  entry against its account before applying any of them.
- Balances only change through `LedgerPostingService`; the account methods that apply entries are
  `internal` to the domain assembly.

## Consequences

- An unbalanced posting is unrepresentable, not just invalid.
- Assets equal liabilities at all times; the trial balance endpoint and domain tests check it.
- Each deposit and withdrawal touches the per-currency settlement account, which becomes a hot
  stream ([ADR-0004](0004-serialised-appends-and-atomic-multi-stream-writes.md)).
- `Money` rejects amounts with more than two decimal places instead of rounding them.

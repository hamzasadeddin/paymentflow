# Phase 03 — Payments, Lifecycle State Machine & Idempotency

> Status: implemented, pending user verification on their machine.
> Builds on Phases 01–02. Delivered as a delta over the existing project folder.

## 1. Goal

Give an Operations Analyst the ability to raise a **payment** that debits a
source `PaymentAccount` and pays an **Approved** `Beneficiary`, then move that
payment through a strict lifecycle. This is the platform's first genuinely
transactional feature, so the emphasis is on:

- a **domain-enforced state machine** (no way to reach an invalid state, even
  by a misbehaving caller);
- **idempotent creation** (a retried request never produces a duplicate
  payment); and
- **explicit money semantics** (when exactly the debit touches
  `AvailableBalance` vs `LedgerBalance`).

Full maker-checker approval policy is **Phase 04**; realistic processing
simulation is **Phase 05**. In Phase 03, `Approve` performs the funds
reservation and the `Process/Complete/Fail` transitions exist on the domain
(ready for Phase 05) but are not yet exposed as API endpoints.

## 2. Lifecycle state machine

```
                         ┌──────────► Cancelled
                         │            (from Draft or PendingApproval)
   Draft ──Submit──► PendingApproval ──Approve──► Approved ──Process──► Processing
                         │                                                │
                         └──Reject──► Rejected                 ┌──────────┴──────────┐
                                                          Complete                 Fail
                                                              │                      │
                                                          Completed               Failed
```

`PaymentStatus`: `Draft(1)`, `PendingApproval(2)`, `Approved(3)`,
`Processing(4)`, `Completed(5)`, `Failed(6)`, `Cancelled(7)`, `Rejected(8)`.

Transitions are explicit methods on the `Payment` entity that throw
`InvalidOperationException` on an invalid source state — identical to the
`Beneficiary` pattern from Phase 02. The Application layer catches that and
returns **409 Conflict**; it is never a 500.

| Transition | From | To | Exposed in Phase 03 API? |
|-----------|------|----|--------------------------|
| `SubmitForApproval` | Draft | PendingApproval | ✅ |
| `Approve` | PendingApproval | Approved | ✅ (scaffold; real policy Phase 04) |
| `Reject` | PendingApproval | Rejected | ✅ |
| `Cancel` | Draft, PendingApproval | Cancelled | ✅ |
| `MarkProcessing` | Approved | Processing | ⛔ domain-only (Phase 05) |
| `Complete` | Processing | Completed | ⛔ domain-only (Phase 05) |
| `Fail` | Processing | Failed | ⛔ domain-only (Phase 05) |

## 3. Money semantics (the key decision)

A payment reserves funds on approval and settles them on completion:

- **On `Approve`** the source account is debited from **`AvailableBalance`**
  (`AvailableBalance -= Amount`). The money is now committed and can no longer
  be spent twice, but the ledger (the settled position) is untouched. This is
  the *reservation*.
- **On `Complete`** (Phase 05) the **`LedgerBalance`** is reduced by `Amount` —
  the ledger catches up with reality once the payment has actually gone out.
- **On `Fail`** (Phase 05) the reservation is released back to
  `AvailableBalance` (`AvailableBalance += Amount`).

Because a payment can only be `Cancel`led or `Reject`ed **before** approval, no
reservation exists yet at those points, so Phase 03 needs no release path. The
release-on-fail and settle-on-complete logic lives on the domain now and will be
wired up in Phase 05.

Balance mutation is coordinated in the **Application handler** (it spans two
aggregates — `Payment` and `PaymentAccount`) rather than inside a single entity,
using dedicated `PaymentAccount` methods (`Reserve`, `ReleaseReservation`,
`Settle`) so the arithmetic and its guards stay in the domain.

## 4. Guards

Checked with `decimal` throughout (never float):

- `Amount > 0`.
- Payment `Currency` is on the ISO allow-list **and** equals the source
  account's currency **and** equals the beneficiary's currency (no FX in this
  demo).
- Beneficiary must be `Approved`.
- Source account must be `Active`.
- **At submit and approve:** `account.CanDebit(Amount)` (Active + funds
  available) **and** the account's **daily limit** — the sum of amounts already
  committed today (Approved/Processing/Completed) from that account, plus this
  amount, must not exceed `DailyLimit`.

"Today" is derived from `IDateTimeProvider.UtcNow.Date`, so tests can pin it.

## 5. Idempotency

- The create endpoint reads an optional **`Idempotency-Key`** HTTP header. If
  present it is used; if absent the server generates a GUID so the column is
  always populated.
- A **unique index** on `IdempotencyKey` plus a **pre-insert lookup** enforces
  it: a repeated create with the same key returns the **original** payment
  (HTTP 200) instead of creating a second one, rather than surfacing a unique
  constraint violation.
- `PaymentReference` (`PAY-{year}-{seq:D6}`) is generated the same way as
  `CUST-…`, with its own unique index.

## 6. Layer-by-layer changes

**Domain** — `Payment : AuditableEntity` (reference, source account id,
beneficiary id, amount, currency, status, description, idempotency key,
review/stamp fields, failure reason) with the transition methods above;
`PaymentStatus` enum; `Reserve/ReleaseReservation/Settle` on `PaymentAccount`.

**Application** — `CreatePaymentCommand` (idempotent),
`GetPaymentsQuery` (paged; filter by status / source account / beneficiary /
date range / search on reference/description), `GetPaymentByIdQuery`,
`SubmitPaymentCommand`, `CancelPaymentCommand`, `TransitionPaymentCommand`
(approve/reject), plus FluentValidation validators. Concurrency conflicts →
409. Reference + idempotency handled here.

**Infrastructure** — `PaymentConfiguration` (money `decimal(19,4)`; unique
indexes on `PaymentReference` and `IdempotencyKey`; indexes on `Status`,
`SourceAccountId`, `BeneficiaryId`; `RowVersion` concurrency token; FKs to
account and beneficiary with `DeleteBehavior.Restrict` so committed payments
pin their references). `Payments` added to `IApplicationDbContext` and the
`PaymentFlowDbContext`. `DemoDataSeeder` extended with a handful of sample
payments across statuses (currency-consistent with the seeded accounts and
beneficiaries).

**Api** — `PaymentsController`: `GET` (paged), `GET /{id}`, `POST` (create,
reads `Idempotency-Key`), `POST /{id}/submit-for-approval`, `POST /{id}/cancel`,
`POST /{id}/approve`, `POST /{id}/reject`. New policies `CanManagePayments`
(Admin + Analyst) and `CanApprovePayments` (Admin + Approver). Masking rules
consistent with Phase 02 (only the masked source account number is returned).

**Angular** — `payment.models.ts`, `payment.service.ts`; a `payments` feature
(list with status filter + pagination + load states; detail; create form that
picks a source account + an Approved beneficiary + amount, with reactive
validation and client-side idempotency-key generation). Payments nav item and
routes enabled. Reuses the shared table / status-chip / masked-value /
confirm-dialog components and the existing design tokens.

## 7. Tests

- **Domain:** state-machine transitions (each valid hop; invalid hops throw);
  reservation arithmetic on `PaymentAccount`.
- **Application:** create/submit validators; currency-mismatch; beneficiary not
  approved; insufficient funds; daily-limit exceeded.
- **Api integration:** create + **idempotency replay returns the same id**;
  analyst-can't-approve → 403; auditor write → 403; submit/cancel happy paths;
  approve reserves funds; invalid transition → 409.
- **Angular:** `payment.service` spec (param building + idempotency header).

## 8. Acceptance checklist

- Build + tests green; new EF migration applies cleanly as a delta.
- Creating a payment twice with the same idempotency key yields **one** payment.
- Currency / funds / beneficiary-status / daily-limit guards enforced.
- Lifecycle transitions validated (invalid → 409).
- Approve reserves from `AvailableBalance`.
- Payments screen lists and creates with masking and load states.
- Role gating correct (manage = Admin/Analyst, approve = Admin/Approver,
  everyone reads, auditor cannot write).

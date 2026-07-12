# Phase 04 — Maker-Checker Approval Engine

> Status: **implemented, pending user verification on their machine.**
> Builds on Phases 01–03. Delivered as a delta over the existing project folder,
> mirroring the CQRS / Result / domain-state-machine patterns.
>
> Verification note: the delivery environment could not reach the .NET 10 SDK, so
> the backend was verified by careful pattern-matching and static review rather
> than `dotnet build` / `dotnet test`; the Angular app builds cleanly. Run
> `dotnet test` and generate the EF migration on your machine (see §6).

## 1. Goal

Turn the lightweight `approve` / `reject` stubs from Phase 03 into a real
**maker-checker approval engine** — the control that stops one person from
both raising *and* releasing money.

Three capabilities:

1. **Configurable approval thresholds** — small payments clear with no review;
   larger payments need one approver; the largest need **two distinct**
   approvers (dual control).
2. **Separation of duties** — the person who created a payment can never be one
   of its approvers, and no approver can count twice toward dual approval.
3. **An Approvals queue** — the (currently disabled) *Approvals* nav item lights
   up: everything `PendingApproval`, for payments **and** beneficiaries, with
   approve / reject + notes and, for dual-control items, visible progress.

Realistic settlement (`Approved → Processing → Completed/Failed`) remains
**Phase 05**; the admin UI to *edit* thresholds remains **Phase 07**. Phase 04
introduces the thresholds as configuration and the engine that enforces them.

## 2. The maker-checker model

Phase 03 treated approval as a single state flip. Phase 04 reframes it around
two facts that must be recorded per payment:

- **who the maker is** (currently unrecorded), and
- **every approval / rejection decision** (currently only the *last* reviewer is
  stamped, which cannot represent dual approval).

So the engine rests on two additions: a **maker stamp** on the payment, and an
append-only **`ApprovalDecision`** record per approve/reject action. The
existing `PaymentStatus` machine is **unchanged** — no new states. "Awaiting a
second approver" is represented as *still `PendingApproval`, with one decision
recorded*, and surfaced as a derived label rather than a stored status. This
keeps Phase 05 (which builds on `Approved`) untouched.

```
                       requiredApprovals resolved at submit-time from policy
                                        │
 Draft ──submit──►  PendingApproval ────┼──(0 required)──► Approved   (auto, funds reserved)
                          │             │
                          │   approve #1 (checker ≠ maker)
                          │        │
                          │        ▼
                          │   PendingApproval        ◄── still pending if 2 required
                          │    (1 of 2 decisions)
                          │        │
                          │   approve #2 (distinct checker)
                          │        ▼
                          └────► Approved  ──► funds reserved from AvailableBalance
                          │
                          └──reject (any checker ≠ maker)──► Rejected
```

Funds are still reserved **once**, at the moment the payment actually reaches
`Approved` (final approval), exactly as in Phase 03 — never on a partial
approval.

## 3. Approval policy & thresholds

A payment's required-approver count is a pure function of its amount and a
policy:

| Band | Condition (default) | Required approvals | Result of `submit` |
|------|---------------------|--------------------|---------------------|
| Auto | `amount < AutoApproveBelow` (1,000) | 0 | Straight to `Approved`, funds reserved, stamped as auto |
| Single | `AutoApproveBelow ≤ amount < DualApprovalAtOrAbove` | 1 | `PendingApproval`, one approver finalizes |
| Dual | `amount ≥ DualApprovalAtOrAbove` (5,000) | 2 | `PendingApproval`, two distinct approvers finalize |

- Thresholds live in **`ApprovalPolicyOptions`**, bound from `appsettings.json`
  (`ApprovalPolicy` section) behind an **`IApprovalPolicyProvider`** abstraction
  in the Application layer. Phase 07 swaps the provider's backing store for a
  DB-backed, admin-editable table **without touching the engine**.
- The resolved requirement is **stamped onto the payment at submit time**
  (`RequiredApprovals`), so a later change to the thresholds never re-scopes an
  in-flight payment — the rule that applied when it was submitted is the rule it
  finishes under. (A small but genuinely bank-grade property.)
- Thresholds are compared against the payment amount **in its own currency**;
  this demo uses a single numeric band rather than per-currency tables (noted as
  a deliberate simplification, consistent with the "no FX" stance of Phase 03).

Default seeded thresholds (`AutoApproveBelow = 1000`, `DualApprovalAtOrAbove =
5000`) are chosen so the existing seed data exercises all three bands: the 250
utilities payment auto-clears, the 1,200 supplier payment needs one approver,
and the 5,000 freight payment needs two.

## 4. Separation of duties

- **Maker stamp.** `Payment` and `Beneficiary` gain `CreatedByUserId`, set from
  `ICurrentUserService.UserId` when the record is created.
- **No self-approval.** At approve/reject, if the acting user equals
  `CreatedByUserId`, the engine returns **`Error.Forbidden` → 403**
  (`payment.selfApprovalNotAllowed`). This is enforced in the Application layer,
  not as a domain `throw`, precisely so it maps to **403** rather than the
  domain-transition **409**.
- **No double-counting.** For dual approval, an approver who has already
  recorded an `Approved` decision on a payment cannot approve it again →
  **`Error.Conflict` → 409** (`payment.alreadyApprovedByUser`).
- **Role separation is retained.** `CanManagePayments` (Admin + Analyst) stays
  distinct from `CanApprovePayments` (Admin + Approver). `Administrator` is
  intentionally in both (a break-glass operator), but the maker≠checker record
  check still stops an Administrator from approving a payment they raised — the
  separation is enforced at the *identity* level, not just the role level.

The same self-approval rule applies to **beneficiaries** (single approval only —
they carry no amount, so no dual-control band).

## 5. Recording decisions — the `ApprovalDecision` entity

An append-only audit record, styled after `SecurityAuditEvent` (no FK, so it can
span subject types and never blocks a delete):

```
ApprovalDecision : BaseEntity
    SubjectType    (enum: Payment = 1, Beneficiary = 2)
    SubjectId      (Guid)
    ApproverUserId (string)
    ApproverEmail  (string?)       // convenience for the queue/audit UIs
    Decision       (enum: Approved = 1, Rejected = 2)
    Notes          (string?)
    DecidedAtUtc   (DateTime)
```

- Indexed on `(SubjectType, SubjectId)` for fast per-subject lookups.
- The set of distinct `Approved` decisions for a payment is the source of truth
  for "how many approvals so far"; `RequiredApprovals` on the payment is the
  target. The queue DTO surfaces both.
- Each decision **also** writes a `SecurityAuditEvent` (`PaymentApproved`,
  `PaymentRejected`, `BeneficiaryApproved`, `BeneficiaryRejected`) so Phase 07's
  audit viewer sees approvals for free.

## 6. Layer-by-layer changes

**Domain**
- `Payment`: add `CreatedByUserId` (string?) and `RequiredApprovals` (int,
  private set). `SubmitForApproval` gains a `requiredApprovals` parameter that
  stamps the field while flipping to `PendingApproval`. `Approve` is unchanged
  in meaning (only ever called to *finalize*); the decision to call it lives in
  the handler once the required count is met.
- `Beneficiary`: add `CreatedByUserId` (string?).
- New `ApprovalDecision` entity + `ApprovalSubjectType` / `ApprovalOutcome`
  enums.

**Application**
- `IApprovalPolicyProvider` + `ApprovalRequirement` (record) — resolves an
  amount → required-approver count.
- Rework `TransitionPaymentCommand` into an **approval engine handler**:
  loads the payment + its decisions; enforces self-approval (403), duplicate
  approver (409), status (409); records an `ApprovalDecision`; on the finalizing
  approval reserves funds and calls `payment.Approve`; on reject calls
  `payment.Reject`. Concurrency conflict → 409 (unchanged).
- `SubmitPaymentCommand`: resolve the requirement from the policy, stamp it, and
  **auto-approve in-band** (reserve + `Approve`, stamped `policy:auto`) when
  `RequiredApprovals == 0`.
- `CreatePaymentCommand` / `CreateBeneficiaryCommand`: accept and store the
  maker id (threaded from the controller's `ICurrentUserService`).
- Mirror the self-approval rule into `TransitionBeneficiaryCommand`.
- New `GetApprovalQueueQuery` (payments + beneficiaries that are
  `PendingApproval`, enriched with maker email + `requiredApprovals` /
  `approvalsReceived`) and `GetPaymentApprovalsQuery` (decision history for a
  payment detail view).
- Extend `PaymentDto` with `createdByUserId`, `requiredApprovals`,
  `approvalsReceived`; add `ApprovalQueueItemDto` and `ApprovalDecisionDto`.

**Infrastructure**
- `ApprovalDecisionConfiguration` (indexes, string lengths, enum→int).
- `ApprovalDecisions` added to `IApplicationDbContext` + `PaymentFlowDbContext`.
- `ApprovalPolicyProvider` (binds `ApprovalPolicyOptions` from config) registered
  in DI; defaults in `appsettings.json`.
- `DemoDataSeeder`: stamp a maker (the analyst demo user) on seeded payments and
  approve them as a *different* user (the approver / admin) so separation of
  duties is visibly satisfied in seed data; add one `PendingApproval` dual-control
  payment with a single decision already recorded, to populate the queue on first
  run. (Requires the demo users to exist first — seeding order already runs
  `DatabaseSeeder` before `DemoDataSeeder`.)
- **Migration:** generated on your machine via
  `dotnet ef migrations add Phase04ApprovalEngine` (adds the `ApprovalDecisions`
  table, `Payment.CreatedByUserId`, `Payment.RequiredApprovals`,
  `Beneficiary.CreatedByUserId`). Integration tests need no migration — they
  build the schema from the model with `EnsureCreatedAsync`.

**Api**
- `ApprovalsController`: `GET /approvals` (unified queue),
  `GET /payments/{id}/approvals` (decision history) — read-gated by
  `CanReadOperations`. Approve/reject stay on `PaymentsController` /
  `BeneficiariesController` under `CanApprovePayments` /
  `CanApproveBeneficiaries`, now passing the acting user through the engine.
- New policy usage only; no new roles.

**Angular**
- Enable the **Approvals** nav item + `/approvals` route.
- `approval.models.ts`, `approval.service.ts`.
- `ApprovalsComponent`: two sections (Payments / Beneficiaries) of pending
  items using the shared table + status-chip; approve/reject via the shared
  confirm-dialog with a notes field; dual-control items show
  `approvalsReceived / requiredApprovals`. Approve/reject buttons appear only
  for users whose roles include an approver policy (client-side affordance;
  server is authoritative).
- Extend `payment.models.ts` with the new fields; add a "Partially approved"
  derived display in `status-maps.ts` for `PendingApproval` items that already
  have one decision.

## 7. Tests

- **Domain:** `SubmitForApproval` stamps `RequiredApprovals`; `Approve` still
  only finalizes from `PendingApproval`; `ApprovalDecision` construction.
- **Application:** policy resolver bands (auto / single / dual); self-approval →
  Forbidden; duplicate approver → Conflict; single-approval finalizes + reserves
  funds; dual-approval stays pending after #1 and finalizes after a *distinct*
  #2; auto-approve band on submit; reject records a decision and → Rejected;
  beneficiary self-approval → Forbidden.
- **Api integration:** analyst creates (maker stamped) → analyst cannot approve
  (403 by policy) → approver approves; admin creates then admin-approves-own →
  403 self-approval; dual-control payment needs two distinct approvers; queue
  endpoint returns pending items with maker + progress; auditor approve → 403.
- **Angular:** `approval.service` spec (queue fetch + approve/reject calls);
  `status-maps` spec extended for the partially-approved label.

## 8. Acceptance checklist

- Build + `dotnet test` green; `dotnet ef migrations add Phase04ApprovalEngine`
  applies cleanly as a delta.
- A payment below the auto-approve threshold clears on submit with funds
  reserved and an auto stamp.
- A single-band payment needs exactly one approver (≠ maker) to reach Approved.
- A dual-band payment needs two **distinct** approvers; the maker and any
  repeat approver are refused (403 / 409 respectively).
- Funds are reserved once, only on final approval.
- Approvals queue lists pending payments **and** beneficiaries with maker and
  progress; approve/reject with notes works and records `ApprovalDecision` +
  `SecurityAuditEvent`.
- Role gating intact (manage = Admin/Analyst, approve = Admin/Approver, auditor
  read-only); self-approval blocked even for Administrators.

## 9. Decisions taken (flagged for review)

These are the forks where I picked a default; easy to change before I build:

- **A — Auto-approve band.** Payments below `AutoApproveBelow` skip review and
  clear on submit. This matches "amounts *above* a limit require approval." The
  alternative is *every* payment needs ≥ 1 approver (simpler; set
  `AutoApproveBelow = 0`). **Default: include the auto band.**
- **B — No new status.** Dual-control progress is derived (`PendingApproval` +
  decision count), not a stored `PartiallyApproved` state, to keep Phase 05
  untouched. **Default: derived.**
- **C — Thresholds in config now, DB in Phase 07.** `appsettings` +
  `IApprovalPolicyProvider` seam, not a DB entity yet. **Default: config seam.**
- **D — Unified queue.** One Approvals screen covers payments *and*
  beneficiaries. **Default: unified.**

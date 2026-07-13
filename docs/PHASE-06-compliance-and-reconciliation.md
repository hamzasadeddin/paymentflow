# Phase 06 — Compliance & Reconciliation

> **Design doc for review — no code yet.** This proposes the Phase 06 vertical
> slice. It mirrors the Phase 05 conventions (config-backed deterministic seams,
> explicit domain transitions → 409, Result/Error, named policies, masked +
> RowVersion projections) and the existing entity/handler/controller shapes.
> Once approved I'll implement domain → application → infrastructure → API →
> Angular, add tests at all three levels, and deliver a zip that stops for review.

---

## 1. Goal

Turn the two stubbed sidebar items — **Compliance** and **Reconciliation** —
into working vertical slices that sit naturally on top of the existing engines:

- **Compliance** — a sanctions/limit **screening + holds** workflow. Payments are
  screened (deterministically, behind a swappable seam) when submitted; a flag
  opens a **compliance case** that **blocks settlement** until a Compliance
  Officer clears it. The Phase 02 role-gated, audited **account-number reveal** is
  surfaced here as a first-class action (no backend change — it already exists).
- **Reconciliation** — match **settled** payments (Phase 05's `Completed` +
  ledger movements) against a **simulated external bank statement**, classify the
  differences as **breaks**, and let an operator **resolve** or **ignore** each.

Both areas reuse the shared shell/table/status-chip/masked-value/confirm-dialog
components and the CQRS + Result/Error + named-policy patterns unchanged.

---

## 2. The compliance model

### 2a. Screening — a deterministic, config-backed seam

Mirroring `ISettlementSimulator` (Phase 05) and `IApprovalPolicyProvider`
(Phase 04), screening is a **pure function** behind an interface, with the rule
in Infrastructure and thresholds in configuration:

```csharp
// Application/Abstractions/IComplianceScreeningService.cs
public enum ComplianceCategory { Sanctions = 1, Limit = 2, Manual = 3 }

public sealed record ScreeningResult(bool Flagged, ComplianceCategory Category, string Reason)
{
    public static ScreeningResult Clear() => new(false, ComplianceCategory.Manual, "");
    public static ScreeningResult Flag(ComplianceCategory category, string reason) => new(true, category, reason);
}

public interface IComplianceScreeningService
{
    ScreeningResult Screen(Payment payment, Beneficiary beneficiary);
}
```

Concrete `RuleBasedComplianceScreeningService` (Infrastructure/Compliance) reads
`ScreeningOptions` (the `Compliance` config section) and flags when **either**:

- the beneficiary name contains a watchlisted term, or the beneficiary
  `CountryCode` is watchlisted → `Sanctions`; or
- the amount is at/above `SinglePaymentReviewLimit` → `Limit`.

The rule is deterministic (no randomness), so demo behaviour is reproducible and
the whole thing is swappable without touching the command handlers.

```jsonc
// appsettings.json — new section
"Compliance": {
  "AutoScreenOnSubmit": true,
  "WatchlistBeneficiaryNames": [ "Gulf Freight" ],
  "WatchlistCountryCodes": [ "IR", "KP", "SY" ],
  "SinglePaymentReviewLimit": 5000
}
```

### 2b. `ComplianceCase` — the hold entity

A hold is a first-class `AuditableEntity` (gets `RowVersion`, so clear/reject is
an optimistic-concurrency operation just like an approval). It is tied to a
payment by id + a denormalized reference snapshot for display — **no FK**, the
same styling as `ApprovalDecision`/`SecurityAuditEvent`, so it can never block a
delete and stays audit-friendly.

```csharp
public enum ComplianceCaseStatus { Open = 1, Cleared = 2, Rejected = 3 }

public class ComplianceCase : AuditableEntity
{
    public Guid PaymentId { get; set; }
    public string PaymentReference { get; set; } = "";       // snapshot for display
    public ComplianceCategory Category { get; set; }
    public string Reason { get; set; } = "";
    public string? RaisedByUserId { get; set; }              // null ⇒ system/auto-screen
    public ComplianceCaseStatus Status { get; private set; } = ComplianceCaseStatus.Open;
    public string? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public string? ReviewNotes { get; private set; }

    public void Clear(string reviewerUserId, string? notes, DateTime utcNow) { /* Open→Cleared, else throw */ }
    public void Reject(string reviewerUserId, string? notes, DateTime utcNow) { /* Open→Rejected, else throw */ }
}
```

Invalid transitions **throw** (e.g. clearing an already-cleared case) → the
Application layer catches and returns **409**, exactly as elsewhere.

### 2c. How a hold gates settlement (no new PaymentStatus)

A hold is a **gate**, not a new payment state. The `Payment` state machine and
the Phase 05 exactly-once claim stay untouched. A payment is **settlement-blocked**
iff it has any `ComplianceCase` whose status is **`Open`** or **`Rejected`**:

- `Open` — awaiting a Compliance Officer's decision.
- `Cleared` — compliance approved; no longer blocks.
- `Rejected` — permanently blocked; operations cancels the payment via the
  existing cancel path (compliance reject does **not** itself move the payment —
  see §9, decision 1).

The gate is enforced in two places already central to Phase 05:

1. **`ProcessPaymentCommand`** — before the `Approved → Processing` claim, if a
   blocking case exists, return `Conflict("payment.onComplianceHold", …)`. This
   sits ahead of `MarkProcessing`, so the RowVersion claim is never even attempted.
2. **`PaymentProcessingWorker`** — its "find Approved payments" query excludes any
   with a blocking case, so the worker simply skips held payments.

### 2d. When screening runs

Auto-screening runs inside **`SubmitPaymentCommand`** (Draft → PendingApproval).
If `AutoScreenOnSubmit` is on and the screen flags, an `Open` `ComplianceCase` is
created in the same unit of work and a `ComplianceHoldPlaced` audit event is
written. The payment **still flows through maker-checker normally** — compliance
and approval are independent controls; the hold only bites at settlement. This
keeps the two engines decoupled (see §9, decision 2).

### 2e. Reveal, surfaced

The Compliance screen exposes the existing `GET /accounts/{id}/reveal-number`
(policy `CanRevealAccountNumbers` = Admin + ComplianceOfficer, already audited via
`AccountNumberRevealed`). **No backend change** — Phase 06 only adds the UI
surface (a "Reveal" action on the case's source account, behind the same role).

---

## 3. The reconciliation model

### 3a. Simulated external statement — a seam

The "bank statement" is produced behind an interface so the source is swappable
(a real integration would drop in here later):

```csharp
public sealed record StatementLine(string Reference, decimal Amount, string Currency, DateTime ValueDateUtc);

public interface IExternalStatementProvider
{
    IReadOnlyList<StatementLine> GetStatement(DateTime asOfUtc);
}
```

`SimulatedStatementProvider` (Infrastructure/Reconciliation) builds the statement
from the **`Completed`** payments (the ledger side is the source of truth), then
applies a **deterministic** perturbation controlled by `ReconciliationOptions` so
every run reproducibly yields one of each break type for the demo:

- **drop** the completed payment whose reference ends in `DropReferenceEndingIn`
  (→ *MissingFromStatement*),
- **add** one phantom line `PHANTOM-{date}` for `PhantomAmount` (→ *MissingFromLedger*),
- **bump** one line's amount by `AmountDriftMinorUnits` (→ *AmountMismatch*).

```jsonc
// appsettings.json — new section
"Reconciliation": {
  "IntroduceSyntheticBreaks": true,
  "DropReferenceEndingIn": "4",
  "PhantomAmount": 999.00,
  "AmountDriftMinorUnits": 50
}
```

With `IntroduceSyntheticBreaks: false` the statement mirrors the ledger exactly
(a clean recon, zero breaks) — useful for the "happy path" test.

### 3b. `ReconciliationRun` + `ReconciliationBreak`

A parent run with child break records (the same parent/child shape as Payment +
ApprovalDecision). Breaks are `AuditableEntity` (RowVersion) so resolve/ignore is
an optimistic-concurrency operation.

```csharp
public sealed class ReconciliationRun : AuditableEntity
{
    public string RunReference { get; set; } = "";           // RECON-2026-000001
    public DateTime StatementDateUtc { get; set; }
    public string? RunByUserId { get; set; }
    public int MatchedCount { get; set; }
    public int BreakCount { get; set; }
    public DateTime CompletedAtUtc { get; set; }
}

public enum BreakType { MissingFromStatement = 1, MissingFromLedger = 2, AmountMismatch = 3 }
public enum BreakStatus { Open = 1, Resolved = 2, Ignored = 3 }

public sealed class ReconciliationBreak : AuditableEntity
{
    public Guid RunId { get; set; }
    public BreakType Type { get; set; }
    public Guid? PaymentId { get; set; }
    public string? PaymentReference { get; set; }
    public string? StatementReference { get; set; }
    public decimal? LedgerAmount { get; set; }
    public decimal? StatementAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public BreakStatus Status { get; private set; } = BreakStatus.Open;
    public string? ResolvedByUserId { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public string? ResolutionNotes { get; private set; }

    public void Resolve(string userId, string? notes, DateTime utcNow) { /* Open→Resolved, else throw */ }
    public void Ignore(string userId, string? notes, DateTime utcNow)  { /* Open→Ignored,  else throw */ }
}
```

### 3c. The matching pass

`RunReconciliationCommand` loads `Completed` payments (ledger side) and the
statement lines (external side), then classifies by `PaymentReference`:

- **Matched** — reference on both sides, amounts equal → counted, no break.
- **MissingFromStatement** — completed payment with no statement line.
- **MissingFromLedger** — statement line with no completed payment.
- **AmountMismatch** — reference on both sides, amounts differ.

It persists one `ReconciliationRun` + N `ReconciliationBreak` rows and writes a
`ReconciliationRunCompleted` audit event. `RunReference` uses the same
`PREFIX-{year}-{seq:D6}` convention as `PaymentReference`.

---

## 4. Concurrency & auditing

- **Compliance holds** and **reconciliation breaks** both carry `RowVersion`;
  clearing/resolving under a race yields the standard
  `DbUpdateConcurrencyException → 409` mapping (as the approval and settlement
  paths do). Two officers clearing the same case → one wins, the other reloads.
- Every state change writes a `SecurityAuditEvent`, extending the Phase 04/05
  trail and feeding directly into the Phase 07 audit-log viewer:
  `ComplianceHoldPlaced`, `ComplianceHoldCleared`, `ComplianceHoldRejected`,
  `ReconciliationRunCompleted`, `ReconciliationBreakResolved`,
  `ReconciliationBreakIgnored`. (The existing `AccountNumberRevealed` covers the
  reveal.)

---

## 5. Layer-by-layer changes

**Domain** — new: `ComplianceCase` (+ `ComplianceCaseStatus`, `ComplianceCategory`),
`ReconciliationRun`, `ReconciliationBreak` (+ `BreakType`, `BreakStatus`), with
explicit throw-on-invalid transition methods. Modified: `SecurityAuditEvent`
gains the six new event-type constants.

**Application** — new abstractions `IComplianceScreeningService`,
`IExternalStatementProvider`; options `ScreeningOptions`, `ReconciliationOptions`.
New features:
- `Features/Compliance/` — `ClearComplianceCaseCommand`, `RejectComplianceCaseCommand`;
  `GetComplianceQueueQuery`, `GetPaymentComplianceCasesQuery`; DTOs + validators.
- `Features/Reconciliation/` — `RunReconciliationCommand`, `ResolveBreakCommand`,
  `IgnoreBreakCommand`; `GetReconciliationRunsQuery`, `GetRunBreaksQuery`; DTOs.

Modified: `SubmitPaymentCommand` (auto-screen → open case), `ProcessPaymentCommand`
(compliance gate before the claim), `PaymentProcessingWorker` (exclude blocked
payments), `IApplicationDbContext` (three new `DbSet`s).

**Infrastructure** — `RuleBasedComplianceScreeningService`,
`SimulatedStatementProvider`; EF `IEntityTypeConfiguration`s for the three new
entities (money `decimal(19,4)`, reference/status indexes); `DependencyInjection`
registers the two seams + binds the two options sections; **one new migration**
`Phase06ComplianceReconciliation`; `DemoDataSeeder` seeds one **open** compliance
case on the flagged dual-control payment so the Compliance queue is populated on
first run (consistent with the seeded partially-approved approval).

**API** — `ComplianceController` (`GET /compliance/cases`,
`GET /payments/{id}/compliance`, `POST /compliance/cases/{id}/clear`,
`POST /compliance/cases/{id}/reject`); `ReconciliationController`
(`POST /reconciliation/run`, `GET /reconciliation/runs`,
`GET /reconciliation/runs/{id}/breaks`, `POST /reconciliation/breaks/{id}/resolve`,
`POST /reconciliation/breaks/{id}/ignore`). New policies (see §6). `Program.cs`
registers the new options/services; `appsettings.json` gains the two sections.
Reveal reuses the existing `AccountsController` endpoint.

**Angular** — enable the `Compliance` + `Reconciliation` nav items and add their
routes. New `compliance.component` (queue table; clear/reject via confirm-dialog;
reveal action on the source account via `masked-value`), `reconciliation.component`
(a "Run reconciliation" button; runs list; a run's breaks with resolve/ignore).
New `compliance.service.ts`, `reconciliation.service.ts` + models. Reuses
status-chip / confirm-dialog / masked-value unchanged.

---

## 6. Authorization (proposed — flagged for review)

Two new named policies, defined the same way as the existing ones:

- **`CanManageCompliance`** = `Administrator` + `ComplianceOfficer` — clear/reject
  holds. (Reveal stays on the existing `CanRevealAccountNumbers` = same two roles.)
- **`CanReconcile`** = `Administrator` + `ComplianceOfficer` + `OperationsAnalyst`
  — run reconciliation, resolve/ignore breaks.

Reading both areas stays on `CanReadOperations` (all roles incl. `ReadOnlyAuditor`),
so the auditor can view the compliance queue and recon breaks read-only but take
no action. Role choices are the one thing most worth a second opinion — say the
word if you'd rather, e.g., keep reconciliation compliance-only.

---

## 7. Tests

**Domain** — `ComplianceCase` transitions (Open→Cleared/Rejected; invalid throws);
`ReconciliationBreak` transitions (Open→Resolved/Ignored; invalid throws);
screening rule purity (watchlist name, watchlist country, limit, clean).

**Application** — screening flags each category and clears the clean case; the
reconcile pass classifies matched / missing-from-statement / missing-from-ledger /
amount-mismatch correctly; the compliance gate blocks `ProcessPaymentCommand`
with an open case and lets it through once cleared.

**Integration** — compliance queue auth (401 anon / 403 wrong role); clearing a
hold then processing settles; a rejected/open hold returns
`payment.onComplianceHold` 409 on process; reveal returns the number and writes
the audit event; a reconciliation run creates the expected breaks; resolving a
break returns 200 and re-resolving returns 409; run/resolve auth (401/403).

Target ≈ 18–22 new cases across the three suites (roughly the Phase 04/05 volume).

---

## 8. Migration & delivery notes

- **This phase adds a migration** (three tables) — unlike Phase 05. Per the
  working agreement I'll **not** touch the existing `Migrations/` folder or the
  model snapshot; I'll deliver the entity configs and you generate the migration
  locally:
  ```
  dotnet ef migrations add Phase06ComplianceReconciliation -p src/PaymentFlow.Infrastructure -s src/PaymentFlow.Api
  dotnet ef database update -p src/PaymentFlow.Infrastructure -s src/PaymentFlow.Api
  ```
  (Or drop/recreate the dev DB so the seeder re-runs with the seeded open case.)
- No new frontend dependencies (unlike Phase 05's SignalR), so `npm install`
  isn't required — but `ng build` still needs network for prod font inlining;
  use `--configuration development` offline.
- **Verification caveat carried from 03–05:** the delivery environment has no
  .NET SDK, so backend is static-reviewed against existing patterns and the
  Angular app is built there; **`dotnet test` on your machine is the real gate.**

---

## 9. Decisions taken (flagged for review)

1. **Compliance reject does not auto-move the payment.** A `Rejected` case keeps
   the payment permanently settlement-blocked; operations then cancels it through
   the existing path. This keeps two state machines from entangling. *Alternative:*
   have reject transition the payment to `Cancelled`/`Rejected` directly — cleaner
   UX, more coupling. Easy to switch if you prefer it.
2. **Screening gates settlement, not approval.** A flagged payment still flows
   through maker-checker; the hold only bites at processing. This keeps compliance
   and approval independent. *Alternative:* block submission/approval outright.
3. **Simulated statement derives from the ledger with deterministic drift.** Keeps
   the demo reproducible and the seam realistic without external I/O. *Alternative:*
   a static seeded statement file — more "external" feeling, less self-consistent.
4. **Reconciliation is on-demand only** (a button / endpoint), no background job.
   Runs are cheap and reviewer-initiated; a scheduled recon can be added later
   the same way the Phase 05 worker was.
5. **Proposed roles** in §6 — the main thing to confirm.

---

## 10. Acceptance checklist (what "done" will look like)

- [ ] Submitting a flagged payment (watchlisted beneficiary or ≥ review limit)
      opens a compliance case and writes `ComplianceHoldPlaced`.
- [ ] A payment with an open/rejected case cannot settle (manual → 409; worker
      skips it); clearing the case lets it settle normally.
- [ ] Compliance Officer / Admin can clear or reject a case; wrong roles get 403;
      re-deciding a closed case gets 409.
- [ ] Reveal works from the Compliance screen for the two allowed roles and writes
      `AccountNumberRevealed`.
- [ ] A reconciliation run produces matched counts + one of each break type (with
      synthetic breaks on) or zero breaks (off); resolve/ignore work and are audited.
- [ ] Compliance + Reconciliation nav items are enabled and routed; the shared
      components are reused unstyled-per-feature.
- [ ] `dotnet test` green (existing 105 + the new Phase 06 cases); Angular builds.

---

## 11. Open questions for you

1. **Roles** (§6) — accept the proposed `CanManageCompliance` /
   `CanReconcile`, or adjust?
2. **Compliance reject behaviour** (§9.1) — leave the payment for operations to
   cancel, or have reject cancel it directly?
3. **Scope check** — anything you'd cut from this round (e.g. defer reconciliation
   resolve-notes, or the reveal surface) to keep the slice tight?

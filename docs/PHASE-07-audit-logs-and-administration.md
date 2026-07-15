# Phase 07 — Audit Logs & Administration

> **Design doc for review — no code yet.** This proposes the Phase 07 vertical
> slice. It mirrors the conventions established through Phases 01–06 (CQRS +
> MediatR, Result/Error, FluentValidation, named policies, explicit domain
> transitions → 409, masked + `RowVersion` projections, config-backed seams) and
> the existing entity/handler/controller/Angular shapes. Once approved I'll
> implement domain → application → infrastructure → API → Angular, add tests at
> all three levels, verify (`dotnet test` + Angular build), and deliver a zip
> that **stops for review**.

---

## 1. Goal

Turn the last two stubbed sidebar items — **Audit logs** and **Administration** —
into working vertical slices, closing out the platform:

- **Audit logs** — a read-only, filterable viewer over the `SecurityAuditEvent`
  trail that every prior phase already writes to (auth, approvals, processing,
  and the Phase 06 compliance/reconciliation events). No new capture logic — the
  events already exist; Phase 07 surfaces them.
- **Administration** — two admin-only areas:
  1. **User & role management** — list users, create a user, activate/deactivate,
     and change role assignments, over the existing ASP.NET Identity store.
  2. **Rules configuration** — move the four config-backed seams
     (`ApprovalPolicy`, `Processing`, `Compliance`/`Screening`,
     `Reconciliation`) out of `appsettings` and into an **admin-editable store**,
     **with no change to the engines** that consume them.

Both areas reuse the shared shell / table / status-chip / masked-value /
confirm-dialog components and the CQRS + Result/Error + named-policy patterns
unchanged. Every administrative action itself writes a `SecurityAuditEvent`, so
the two areas reinforce each other: admin changes show up in the audit log.

---

## 2. Scope & non-goals

**In scope:** the two screens above; a paged/filtered audit query; a small
user-admin service over `UserManager`; a `RuleSettings` store plus a store-backed
re-wiring of the four providers with config fallback; new named policies; a
Phase 07 migration (one new table); tests at all three levels.

**Non-goals (deliberate, tracked in §11):** editing/deleting audit events (the
trail stays append-only); external log shipping/SIEM; self-service password reset
by end users (admin-initiated reset only); FX/multi-currency threshold handling
(unchanged no-FX stance); live per-connection push of admin changes over SignalR;
per-field rule versioning history beyond "who last changed it, when."

---

## 3. Audit logs viewer

### 3a. The event trail today (recap, no change)

`SecurityAuditEvent : BaseEntity` is append-only (no `RowVersion` — never edited)
and is already a `DbSet` on `IApplicationDbContext`. It carries `UserId`, `Email`,
`EventType`, `Succeeded`, `IpAddress`, `Details`, `OccurredAtUtc`. Write sites
already exist across Identity, payments, approvals, processing, compliance, and
reconciliation. The event-type vocabulary lives in `SecurityEventTypes`.

### 3b. Query — a paged, filterable read

A single query mirrors the existing paged-list shape (`PagedRequest` /
`PagedResult<T>`), adding audit-specific filters:

```csharp
// Application/Features/Audit/AuditQueries.cs
public record GetAuditEventsQuery(
    int Page = 1,
    int PageSize = 25,
    string? EventType = null,        // exact match against SecurityEventTypes
    bool? Succeeded = null,          // success/failure filter
    string? UserQuery = null,        // matches Email (contains, case-insensitive)
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? Search = null            // free-text over Details + Email
) : IRequest<Result<PagedResult<AuditEventDto>>>;
```

The handler filters server-side, orders by `OccurredAtUtc` **descending** (newest
first), pages, and projects to a DTO. No masking is required — audit rows never
store raw account numbers (reveals are recorded as an *event*, not the number) —
so this stays a straight translatable projection with no in-memory step.

```csharp
public record AuditEventDto(
    Guid Id, Guid? UserId, string? Email, string EventType, bool Succeeded,
    string? IpAddress, string? Details, DateTime OccurredAtUtc);
```

### 3c. Event-type catalog for the filter

To keep the client filter dropdown from drifting from the backend vocabulary, a
tiny read exposes the known event types grouped by area (Auth, Payments,
Approvals, Compliance, Reconciliation, Administration):

```
GET /api/v1/audit-events/event-types  ->  { group: string, types: string[] }[]
```

Derived from the `SecurityEventTypes` constants (extended in §4c), so adding an
event type in code automatically surfaces in the filter.

### 3d. Endpoint & policy

```
GET /api/v1/audit-events              (paged, filtered)
GET /api/v1/audit-events/event-types  (filter catalog)
```

New named policy **`CanReadAuditLog`** = `Administrator` + `ComplianceOfficer` +
`ReadOnlyAuditor`. This is intentionally *narrower* than `CanReadOperations`:
the audit trail is sensitive, and operations/approver roles don't need it, but
the read-only auditor (whose whole purpose is oversight) and compliance do. See
§9, decision 1.

### 3e. Angular screen (`features/audit-logs`)

A filter bar (event-type select fed by the catalog, success/failure toggle, a
date-range, a search box) above the shared table, with server-side paging. Each
row shows time, actor (email), event type as a `status-chip` (success = neutral/
green, failure = warn), IP, and details. Read-only — no row actions. Reachable by
Admin, Compliance, and the Read-Only Auditor via the `role.guard`.

---

## 4. Administration — user & role management

### 4a. Surface

`ApplicationUser : IdentityUser<Guid>` already carries `DisplayName`, `IsActive`,
`CreatedAtUtc`; roles are the five `ApplicationRole`s. Phase 07 adds a thin
**`IUserAdminService`** abstraction (Application) implemented over `UserManager` /
`RoleManager` (Infrastructure) — mirroring how `IIdentityService` wraps Identity
today, so the Application layer stays free of Identity types.

```csharp
// Application/Abstractions/IUserAdminService.cs
public interface IUserAdminService
{
    Task<Result<PagedResult<AdminUserDto>>> ListAsync(PagedRequest request, CancellationToken ct);
    Task<Result<AdminUserDto>> CreateAsync(CreateUserRequest request, string actingUserId, CancellationToken ct);
    Task<Result> SetActiveAsync(Guid userId, bool isActive, string actingUserId, CancellationToken ct);
    Task<Result<AdminUserDto>> SetRolesAsync(Guid userId, IReadOnlyList<string> roles, string actingUserId, CancellationToken ct);
    Task<Result> ResetPasswordAsync(Guid userId, string newPassword, string actingUserId, CancellationToken ct);
}
```

`AdminUserDto` = `Id, Email, DisplayName, IsActive, Roles[], CreatedAtUtc`.
Roles are validated against `Roles.All`; unknown role → `Validation` (400).

### 4b. Guard rails (banking-flavoured)

- **No self-lockout / no last-admin removal.** An administrator cannot deactivate
  their own account, nor remove the last remaining `Administrator`, nor strip
  their own `Administrator` role → `Conflict` (409) with a clear code. This is the
  admin analogue of separation-of-duties.
- **Deactivation is immediate.** `IsActive=false` already blocks login *and*
  refresh (both check `user.IsActive` in `IdentityService`), so a deactivated
  user's existing session dies at the next 15-minute access-token expiry. No
  engine change needed — the check already exists.
- Password reset is admin-initiated only, uses `UserManager`'s reset-token path,
  and never returns or logs the password.

### 4c. New audit events

Add to `SecurityEventTypes` (Administration group), written by the service on each
mutation (actor = current user, subject in `Details`):

```
UserCreated, UserActivated, UserDeactivated, UserRolesChanged, UserPasswordReset,
RuleSetUpdated
```

These flow straight into the Phase 07 audit viewer — administration is itself
audited.

### 4d. Endpoints & policy

```
GET   /api/v1/admin/users
POST  /api/v1/admin/users
POST  /api/v1/admin/users/{id}/activate
POST  /api/v1/admin/users/{id}/deactivate
PUT   /api/v1/admin/users/{id}/roles
POST  /api/v1/admin/users/{id}/reset-password
```

All gated by **`CanAdminister`** = `Administrator` only.

### 4e. Angular screen (`features/admin`, Users tab)

A users table (email, display name, roles as chips, active toggle, created) with a
"New user" dialog and a role-assignment dialog, using the shared confirm-dialog
for activate/deactivate. Admin-only via `role.guard`.

---

## 5. Administration — rules configuration

This is the phase's one piece of real architecture: making the four config-backed
seams **admin-editable at runtime without touching the engines**.

### 5a. The seam today

Each engine already consumes its rules through an abstraction or `IOptions<T>`,
registered in `Infrastructure/DependencyInjection`:

| Rule set        | Consumed by (engine)                         | Options type            | Section          |
|-----------------|----------------------------------------------|-------------------------|------------------|
| Approval bands  | `IApprovalPolicyProvider` (approval handlers)| `ApprovalPolicyOptions` | `ApprovalPolicy` |
| Screening       | `IComplianceScreeningService` (submit)       | `ScreeningOptions`      | `Compliance`     |
| Reconciliation  | `IExternalStatementProvider` (run)           | `ReconciliationOptions` | `Reconciliation` |
| Processing      | `ISettlementSimulator` + worker              | `ProcessingOptions`     | `Processing`     |

The crucial property: the **engines** (the MediatR handlers and the worker) depend
on the *interfaces*, never on `IOptions<T>` directly. So the store swap changes
only the **provider implementations** and DI wiring — no handler, no state
machine, no controller in the payment/approval/compliance/reconciliation slices
changes.

### 5b. The `RuleSettings` store

One small table, one row per section, JSON-valued, with optimistic concurrency:

```csharp
// Domain/Entities/RuleSetting.cs
public class RuleSetting : AuditableEntity          // gets RowVersion
{
    public string Section { get; set; } = "";        // e.g. "ApprovalPolicy" (unique)
    public string ValueJson { get; set; } = "";      // serialized options object
    public string? UpdatedByUserId { get; private set; }
    public void Apply(string valueJson, string userId, DateTime utcNow) { /* set + stamp */ }
}
```

`AuditableEntity` gives it `RowVersion`, so an edit is an optimistic-concurrency
operation (concurrent admins editing the same section → **409**), exactly like a
compliance clear or an approval. A unique index on `Section` keeps it one-row-per-
section.

### 5c. Effective-options resolution (store, then config fallback)

A new Application abstraction returns the **effective** options for a section —
the stored override if present, otherwise the `appsettings`-bound default:

```csharp
// Application/Abstractions/IRuleSettingsProvider.cs
public interface IRuleSettingsProvider
{
    TOptions GetEffective<TOptions>(string section, TOptions configFallback) where TOptions : class;
}
```

The four provider implementations change their constructor dependency from
`IOptions<TOptions>` to `IRuleSettingsProvider` + the same `IOptions<TOptions>`
(now used only as the fallback). For example:

```csharp
// Infrastructure/Approvals/ApprovalPolicyProvider.cs  (impl only — interface unchanged)
public ApprovalRequirement Resolve(decimal amount)
{
    var opts = _rules.GetEffective(ApprovalPolicyOptions.SectionName, _configFallback.Value);
    return new(RequiredApprovalsFor(amount, opts));
}
```

`IApprovalPolicyProvider.Resolve`, `IComplianceScreeningService.Screen`,
`IExternalStatementProvider`, and the settlement simulator keep their public
shapes; only how they *fetch* their options changes. The approval engine that
calls `Resolve` is untouched — which is the whole point.

**Processing note.** The background `PaymentProcessingWorker` reads
`ProcessingOptions` once. To make `AutoProcessEnabled` / cadence / `FailOnCents`
editable live, the worker re-reads effective options **at the top of each poll
tick** (it already loops) rather than capturing them at construction. This is a
small, contained change to the worker loop; the settlement *logic* is unchanged.
If we'd rather not touch the worker in this phase, the fallback is to make only
the three request-scoped rule sets (Approval, Screening, Reconciliation) live and
leave Processing config-only — see §9, decision 3.

### 5d. Caching & invalidation

The store is read on the hot path (every submit screens, every approve resolves
bands), so `IRuleSettingsProvider` caches deserialized options in `IMemoryCache`
keyed by section, invalidated on any successful `PUT`. Cache miss → single indexed
read by `Section`. At demo scale this is negligible, and it keeps the engines'
timing characteristics unchanged.

### 5e. Endpoints, validation & policy

```
GET  /api/v1/admin/rules              -> all four rule sets: effective values,
                                         `isOverridden`, updatedBy/at, RowVersion
PUT  /api/v1/admin/rules/{section}    -> validate, persist (RowVersion → 409),
                                         audit `RuleSetUpdated`, invalidate cache
```

Each section has a **FluentValidation** validator enforcing the invariants the
options types imply, e.g.:

- Approval: `AutoApproveBelow >= 0`, `DualApprovalAtOrAbove >= AutoApproveBelow`.
- Screening: `SinglePaymentReviewLimit >= 0`; country codes are 2-letter; no empty
  watchlist entries.
- Reconciliation: `AmountDriftMinorUnits >= 0`; `PhantomAmount >= 0`;
  `DropReferenceEndingIn` is a single digit or empty.
- Processing: `PollingIntervalSeconds >= 1`; `BatchSize >= 1`;
  `0 <= SimulatedLatencyMsMin <= SimulatedLatencyMsMax`; `FailOnCents` in 0–99.

Invalid → `Validation` (400). Gated by **`CanAdminister`** (Administrator only).
A stale `RowVersion` → `Conflict` (409), consistent with the rest of the API.

### 5f. Angular screen (`features/admin`, Rules tab)

Four cards, one per rule set, each a small reactive form pre-filled with the
effective values, a "Reset to default" affordance (clears the override), and a
save that surfaces validation (400) and concurrency (409) inline. A subtle badge
marks a section as *Overridden* vs *Using defaults*. Admin-only.

---

## 6. Data model & migration

- **New entity:** `RuleSetting` (table `RuleSettings`, unique index on `Section`,
  `RowVersion` concurrency token, `decimal` unaffected — value is JSON text).
- **No change** to `SecurityAuditEvents` (already present) or the Identity tables
  (`ApplicationUser.IsActive` / `CreatedAtUtc` already exist).
- **Migration:** `2026071x_Phase07AuditAdministration` — adds `RuleSettings` only.
  Generated locally with `dotnet ef migrations add` per the working agreement;
  the existing `Migrations/` folder and model snapshot are left untouched in the
  delivery and regenerated on the dev machine.
- **Seeding:** `RuleSettings` seeds **empty** (every section falls back to
  `appsettings`), so a fresh DB behaves exactly as Phase 06 until an admin edits a
  rule. Demo data is otherwise unchanged.

---

## 7. API surface additions

New controllers: **`AuditController`** (`/audit-events`, `/audit-events/event-types`)
and **`AdminController`** (`/admin/users…`, `/admin/rules…`). Everything else in
the Phase 06 surface is unchanged.

## 8. Named policies additions

- **`CanReadAuditLog`** = Administrator + ComplianceOfficer + ReadOnlyAuditor.
- **`CanAdminister`** = Administrator only (covers both user management and rules).

Added to `AuthorizationPolicies` alongside the existing intents; no existing
policy changes.

---

## 9. Key decisions

1. **Audit log is not `CanReadOperations`.** A dedicated `CanReadAuditLog` keeps
   the trail visible to oversight roles (auditor, compliance) and admins, but not
   to every operational role. The Read-Only Auditor becomes a first-class consumer
   rather than an also-ran.
2. **One `CanAdminister` policy, not per-action.** User management and rules are
   both administrator-only; a single intent is simpler and matches the current
   role model. Splitting into `CanAdministerUsers` / `CanAdministerRules` later is
   trivial (both already route through the policy seam).
3. **Store-backed providers, config as fallback — engines untouched.** Making the
   *implementations* read effective options (store → config) is the smallest change
   that satisfies "admin-editable with no change to the engines." The alternative
   (a custom `IConfigurationSource` that reloads) is more magical and harder to
   test; the explicit provider approach keeps behaviour obvious and unit-testable.
4. **Worker re-reads options per tick.** Preferred so `AutoProcessEnabled` and
   cadence are live (a nice demo: flip processing off from the admin screen).
   Contained to the loop; settlement logic unchanged. Fallback available if we
   want to keep the worker completely untouched this phase.
5. **Administration is audited via the existing trail**, not a separate table —
   reusing `SecurityAuditEvent` keeps one unified, filterable history and makes the
   two Phase 07 areas reinforce each other.

---

## 10. Test plan (all three levels, mirroring Phase 06's +28)

**Domain** — `RuleSetting` transitions: `Apply` stamps `UpdatedByUserId`/time and
bumps state; round-trips JSON; guards empty section. (Small — most value is above.)

**Application** —
- Rule validators: approval band ordering, non-negative limits, latency min≤max,
  country-code shape, single-digit drop rule.
- `GetAuditEventsQuery` handler: event-type/success/date/search filters compose;
  newest-first ordering; paging math.
- User-admin guard rails: can't deactivate self; can't remove last admin; unknown
  role rejected.
- `IRuleSettingsProvider`: store override wins; empty store falls back to config;
  cache invalidates on write.

**Integration** (new `AuditEndpointsTests`, `AdminUserEndpointsTests`,
`AdminRulesEndpointsTests`) —
- Audit: paged list returns seeded/known events; filter by type and by success;
  `CanReadAuditLog` — auditor **200**, plain analyst **403**; event-type catalog
  shape.
- Admin users: create → appears in list and can log in; deactivate → subsequent
  login **401**; role change reflected in a fresh token's roles; self-deactivate
  **409**; non-admin **403**.
- Admin rules: `GET` returns four sections with fallback values; `PUT` lowering
  `AutoApproveBelow` changes the **observed approval requirement** on the next
  create (end-to-end proof the engine reads the store); stale `RowVersion`
  **409**; invalid payload **400**; non-admin **403**; `RuleSetUpdated` shows up
  in the audit endpoint.

Harness notes carry over unchanged (JWT env in `ApiFactory` static ctor; SQLite
swap; non-parallel; seeded roles+users+business data). The rules tests set/reset
overrides within a test to avoid cross-test bleed, since the store is shared
state in the test host.

---

## 11. Out of scope / follow-ups (none block delivery)

- **Rule-change history** beyond "last updated by/at" — the `RuleSetUpdated` audit
  events already give a full timeline; a diff view is a later nicety.
- **Live push of admin changes** — rules take effect on next read (cache TTL/
  invalidation); no SignalR broadcast this phase (consistent with the Phase 06
  gap that the hub carries processing events only).
- **Bulk user import / SSO / self-service reset** — out of scope for the demo.
- **Count-based reference fragility** (`PAY-`/`RECON-`) — still tracked from
  Phase 06 §8; unrelated to this phase but worth doing before real volume.

---

## 12. Delivery checklist

Domain (`RuleSetting`) → Application (audit query, user-admin + rules commands/
queries, validators, `IRuleSettingsProvider`) → Infrastructure (store impl,
provider re-wiring, `IUserAdminService`, config, migration generated locally) →
API (`AuditController`, `AdminController`, two policies) → Angular (enable the two
sidebar items + routes under `role.guard`; `audit-logs` and `admin` features;
`audit`/`admin` models + services) → tests at all three levels → `dotnet test`
(dev machine) + Angular dev build here → zip, **stop for review**.

# Phase 05 — Simulated Payment Processing

> Status: **implemented, pending user verification on their machine.**
> Builds on Phases 01–04. Delivered as a delta over the existing project folder,
> mirroring the CQRS / Result / domain-state-machine patterns.
>
> Verification note: the delivery environment has **no .NET 10 SDK** (only
> Node 22 / npm 10), so — as in Phases 03–04 — the backend was verified by
> careful static review and pattern-matching rather than `dotnet build` /
> `dotnet test`, and the Angular app was built with `ng build` (it compiles;
> production font-inlining needs network access the sandbox lacks). Run
> `dotnet test` on your machine to confirm. No new EF migration is required
> (Phase 05 is behaviour-only over the Phase 04 schema).

## 1. Goal

Turn an **Approved** payment into settled (or failed) money by driving the
already-present domain transitions:

```
Approved → Processing → Completed
                     ↘ Failed
```

The transition methods (`MarkProcessing`, `Complete`, `Fail`) and the money
semantics (`Settle` on complete, `ReleaseReservation` on fail) already exist on
`Payment` / `PaymentAccount` and are exercised today only by `DemoDataSeeder`.
Phase 05 formalises that logic into a real, testable settlement path and exposes
it two ways, with live status in the UI.

Three capabilities:

1. **Automatic processing** — a background worker continuously picks up
   `Approved` payments and settles them, so the queue drains on its own.
2. **Manual processing** — an operator can trigger settlement for a single
   payment on demand (useful for demos and for reprocessing).
3. **Real-time status** — status changes are pushed to the browser over
   **SignalR**, so the Payments list, the payment detail view, the Approvals
   queue, and the Dashboard update live without polling.

Per the scoping decisions: **both** triggers, a **deterministic** failure rule,
and **SignalR** push.

## 2. The processing model

A single settlement routine is the **one source of truth**, called by both the
worker and the manual endpoint so behaviour can never diverge:

1. Load the payment with its `SourceAccount` (tracked, with `RowVersion`).
2. Guard: status must be `Approved`. Anything else → no-op / `409` (see §5).
3. `payment.MarkProcessing(clock.UtcNow)` — this flip to `Processing` is also the
   **claim**: it is the write that the optimistic-concurrency check protects, so
   only one caller can ever take a given payment into processing (see §5).
4. Simulate settlement latency (`SimulatedLatency`, configurable).
5. Evaluate the **deterministic failure rule** (§4):
   - **Success** → `account.Settle(amount)` then `payment.Complete(utcNow)`.
     `Settle` reduces `LedgerBalance`; the reservation booked at approval is
     consumed.
   - **Failure** → `account.ReleaseReservation(amount)` then
     `payment.Fail(reason, utcNow)`. The reserved funds return to
     `AvailableBalance`; `FailureReason` is stamped.
6. Persist, then publish a `PaymentStatusChanged` notification (§6) for **both**
   the `Processing` and the terminal (`Completed`/`Failed`) transitions.

Money invariants (unchanged from Phase 03, restated so the tests can assert
them): a completed payment lowers `LedgerBalance` by `Amount` and leaves
`AvailableBalance` where approval already put it; a failed payment restores
`AvailableBalance` by `Amount` and leaves `LedgerBalance` untouched. No payment
ever settles twice.

## 3. Triggering — worker + manual

### 3a. Background worker

`PaymentProcessingWorker : BackgroundService` (registered via
`AddHostedService`) in the Infrastructure layer. On each tick it:

- opens its **own DI scope** (a hosted service is a singleton; the
  `DbContext` and handlers are scoped), then
- fetches a bounded batch of `Approved` payment ids (oldest first, `Take(BatchSize)`), then
- runs each through the shared settlement routine, isolating failures so one bad
  payment can't stop the loop, then
- waits `PollingInterval` (using `PeriodicTimer`) before the next tick.

Controlled by `ProcessingOptions` (bound from `appsettings.json`):

| Setting | Default | Meaning |
|---|---|---|
| `AutoProcessEnabled` | `true` | Master switch for the background worker. |
| `PollingIntervalSeconds` | `5` | Delay between worker ticks. |
| `BatchSize` | `10` | Max payments claimed per tick. |
| `SimulatedLatencyMsMin` / `SimulatedLatencyMsMax` | `750` / `2500` | Random per-payment settlement delay (deterministic *outcome*, non-deterministic *timing* — timing does not affect success/failure). |
| `FailOnCents` | `13` | Deterministic failure sentinel (§4). |

Turning `AutoProcessEnabled` off leaves the manual endpoint fully functional —
handy for a demo where you want to step through settlement by hand.

### 3b. Manual endpoint

`POST /api/v1/payments/{paymentId}/process`, guarded by
`AuthorizationPolicies.CanManagePayments` (i.e. `OperationsAnalyst` /
`Administrator` — the same policy that creates and submits payments; approval
policies are deliberately *not* reused, keeping "process" an operations action
rather than an approval action).

It dispatches a new `ProcessPaymentCommand` through MediatR and maps the
`Result` exactly like the other endpoints: `Approved`→`200` with the updated
`PaymentDto`, wrong status → `409` (`payment.invalidTransition`), not found →
`404`, forbidden → `403`.

## 4. Deterministic failure rule

Failures must be **reproducible on demand** (the scoping choice), so the outcome
is a pure function of the payment, not a dice roll:

> A payment **fails** processing iff the cents component of its `Amount`
> equals the configured `FailOnCents` sentinel (default **13**).
> Every other payment **completes**.

So `100.00`, `4999.50` → complete; `100.13`, `5000.13` → fail. This is trivial to
trigger in a demo ("raise a payment for `250.13` and watch it fail"),
configurable, and needs no schema or extra fields. The failure reason recorded is
`"Simulated settlement failure (deterministic rule: cents == 13)."`.

The rule lives behind a small `ISettlementSimulator` abstraction
(`SettlementDecision Decide(Payment payment)`), so the policy can be swapped
(e.g. to a "sentinel beneficiary always fails" rule) without touching the worker,
the command handler, or the tests that cover the settlement mechanics.

> Flagged for review (§9): the sentinel value and the "cents == N" formulation.
> An alternative is a dedicated demo beneficiary that always fails; happy to
> switch.

## 5. Concurrency & idempotency

The worker and the manual endpoint can race, and the worker batch can overlap a
slow previous tick. Two guards keep settlement exactly-once:

1. **Status guard.** The settlement routine only proceeds from `Approved`; the
   domain `MarkProcessing` throws on any other status and the handler maps that
   to `409`. A payment already `Processing`/`Completed`/`Failed` is a no-op for
   the worker and a `409` for the manual caller.
2. **Optimistic concurrency.** `Payment.RowVersion` (already configured) means
   two callers that both read the same `Approved` row will collide on save; the
   loser catches `DbUpdateConcurrencyException`, treats it as "someone else took
   it", and moves on. The `Approved → Processing` write is therefore the atomic
   claim — no distributed lock needed.

Net effect: `Settle` / `ReleaseReservation` run once per payment, so balances
stay correct even under concurrent triggers.

## 6. Real-time status over SignalR

- **Hub.** `PaymentsHub : Hub` mapped at `/hubs/payments`, `[Authorize]`. It only
  broadcasts server → client (no client-invokable methods), so there is no new
  input surface to secure beyond authentication.
- **JWT for WebSockets.** The bearer token can't ride in a header on the
  WebSocket handshake, so JWT bearer options are extended to read the
  `access_token` query-string parameter for requests to `/hubs/*` (the standard
  SignalR pattern). CORS already allows the frontend origin; `AllowCredentials`
  is confirmed for the hub.
- **Application stays UI-agnostic.** The command handler and worker depend on an
  `IPaymentNotificationService` abstraction (Application layer); the SignalR
  implementation lives in Infrastructure/Api. This keeps the settlement code
  free of any transport concern and trivially testable with a fake notifier.
- **Event.** `PaymentStatusChanged { PaymentId, PaymentReference, Status,
  FailureReason?, UpdatedAtUtc }`, pushed on `Processing`, `Completed`, and
  `Failed`. (Approve/reject/submit can publish the same event later; Phase 05
  wires the processing transitions.)
- **Client.** An Angular `PaymentsHubService` using `@microsoft/signalr` opens an
  authenticated connection, exposes an observable/signal stream of status
  changes, and auto-reconnects. The Payments list & detail, the Approvals queue,
  and the Dashboard counters subscribe and update their signals in place — no
  manual refresh.

## 7. Layer-by-layer changes

**Domain** — none. `MarkProcessing` / `Complete` / `Fail` and the account money
methods already exist; Phase 05 only *calls* them. No schema change, **no new
migration**.

**Application**
- `Features/Payments/ProcessPaymentCommand.cs` (+ handler): the shared settlement
  routine described in §2, returning `Result<PaymentDto>`.
- `Abstractions/ISettlementSimulator.cs` — deterministic outcome policy (§4).
- `Abstractions/IPaymentNotificationService.cs` — transport-agnostic notifier
  (§6).
- `Common/ProcessingOptions.cs` — bound options (§3a).

**Infrastructure**
- `Processing/PaymentProcessingWorker.cs` — the `BackgroundService` (§3a).
- `Processing/DeterministicSettlementSimulator.cs` — implements
  `ISettlementSimulator`.
- `Realtime/SignalRPaymentNotificationService.cs` — implements
  `IPaymentNotificationService` over `IHubContext<PaymentsHub>`.
- `DependencyInjection.cs` — `Configure<ProcessingOptions>`, register the
  simulator + notifier, `AddHostedService<PaymentProcessingWorker>()`.
- `DemoDataSeeder.cs` — refactor its inline settlement block to call the shared
  simulator/routine so seed data and live processing share one rule.

**Api**
- `PaymentsHub.cs` + `app.MapHub<PaymentsHub>("/hubs/payments")`.
- `Program.cs` — `AddSignalR()`, JWT `access_token` query-string handling for
  `/hubs/*`, CORS credentials for the hub.
- `PaymentsController.Process` — the manual endpoint (§3b).

**Frontend**
- `core/realtime/payments-hub.service.ts` — SignalR client wrapper.
- `@microsoft/signalr` dependency added to `package.json`.
- Payments list/detail, Approvals, and Dashboard subscribe to live updates; a
  **Process** action (button) on eligible `Approved` payments for
  `OperationsAnalyst` / `Administrator`.
- `environment.ts` — hub URL.

## 8. Tests

- **Domain (unit).** Settlement mechanics: `Complete` lowers `LedgerBalance` and
  not `AvailableBalance`; `Fail` restores `AvailableBalance` and not
  `LedgerBalance`; invalid transitions (e.g. `Complete` from `Approved`) throw.
- **Application (unit).** `ProcessPaymentCommandHandler`: approved→completed happy
  path settles once; a `.13` amount releases the reservation and fails with the
  expected reason; processing a non-`Approved` payment returns `409`; a fake
  `IPaymentNotificationService` receives the expected events. Deterministic
  simulator: `.13` → fail, everything else → complete.
- **Integration / API.** Manual endpoint: `401` unauthenticated, `403` for a
  read-only role, `200` + `Completed` for an ops user on an approved payment,
  `409` on wrong status. A processing-routine test asserts exactly-once settlement
  under two concurrent calls (one wins, one gets the concurrency no-op).
- **Frontend.** `PaymentsHubService` maps a pushed event to a state update; the
  payments list reflects a `Completed`/`Failed` push without a reload.

## 9. Decisions taken (flagged for review)

1. **Deterministic rule = cents-equals-sentinel (default 13).** Reproducible and
   demo-friendly; configurable; alternative "sentinel beneficiary" available.
2. **Manual `process` uses `CanManagePayments`**, not an approval policy —
   processing is an operations action. Approval and settlement stay separate
   authorities.
3. **Worker claims via status + `RowVersion`**, not a distributed lock or a
   queue — appropriate for a single-instance demo and keeps the stack simple.
4. **SignalR broadcasts to all authenticated ops users** (no per-customer
   groups) — matches the current ops-console model; grouping can come later.
5. **No new migration** — Phase 05 is behaviour-only over the Phase 04 schema.

## 10. Acceptance checklist

- [ ] Approved payments settle automatically within ~one polling interval.
- [ ] `POST /payments/{id}/process` settles on demand with correct auth (401/403/409/200).
- [ ] `.13` payments fail deterministically and release their reservation; others complete and settle.
- [ ] No payment settles twice under concurrent worker + manual triggers; balances stay correct.
- [ ] Status changes appear live in the UI (payments list + dashboard feed) via SignalR, with reconnect.
- [ ] `AutoProcessEnabled=false` disables the worker while the manual path still works.
- [ ] Backend static-reviewed; Angular app builds; `dotnet test` green on a .NET 10 machine.

## 11. Implementation notes (deviations from the plan)

Two small, deliberate departures from §7, flagged for transparency:

1. **The demo seeder was left curating payment states directly** rather than
   routed through `ISettlementSimulator`. The seeder intentionally fabricates a
   spread of Completed/Failed demo payments regardless of amount; forcing it
   through the cents-sentinel rule would change which seeded payments fail and
   make the demo data less illustrative. Live processing and the seeder therefore
   share the same *transition* methods but not the same *outcome* policy — the
   simulator governs live settlement only.
2. **Live UI wiring covers the Payments list and a new Dashboard activity feed.**
   Processing events (`Processing`/`Completed`/`Failed`) act on Approved payments,
   which are not in the Approvals (pending) queue, so wiring the Approvals screen
   to the current hub events would be inert. Extending the hub to also broadcast
   approve/reject/submit events — and lighting up Approvals live — is a natural,
   small follow-up.

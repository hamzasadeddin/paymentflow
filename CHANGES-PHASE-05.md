# Phase 05 — Simulated Payment Processing · change manifest

Drop this delta over your existing `paymentflow/` tree (same relative paths).
25 files: 12 new, 13 modified. **No new EF migration** — Phase 05 is
behaviour-only over the Phase 04 schema.

After copying, from `frontend/paymentflow-web/` run `npm install` (a new
dependency, `@microsoft/signalr`, was added), then verify:

```
dotnet test
cd frontend/paymentflow-web && npm run build
```

## New files (12)

### Application
- `src/PaymentFlow.Application/Common/ProcessingOptions.cs` — bound `Processing` config.
- `src/PaymentFlow.Application/Abstractions/ISettlementSimulator.cs` — deterministic outcome seam + `SettlementDecision`.
- `src/PaymentFlow.Application/Abstractions/IPaymentNotificationService.cs` — transport-agnostic push seam + `PaymentStatusChangedNotification`.
- `src/PaymentFlow.Application/Features/Payments/ProcessPaymentCommand.cs` — the shared settlement routine (two-phase claim: Approved→Processing→Completed/Failed).

### Infrastructure
- `src/PaymentFlow.Infrastructure/Processing/DeterministicSettlementSimulator.cs` — fails iff cents == `FailOnCents` (default 13).
- `src/PaymentFlow.Infrastructure/Processing/PaymentProcessingWorker.cs` — `BackgroundService` draining Approved payments.

### API
- `src/PaymentFlow.Api/Hubs/PaymentsHub.cs` — authenticated, server→client SignalR hub.
- `src/PaymentFlow.Api/Realtime/SignalRPaymentNotificationService.cs` — `IPaymentNotificationService` over `IHubContext`.

### Tests
- `tests/PaymentFlow.Domain.Tests/PaymentProcessingTests.cs` — processing transitions + account money effects.
- `tests/PaymentFlow.Api.IntegrationTests/PaymentProcessingEndpointsTests.cs` — manual endpoint (auth/happy/fail/409/exactly-once) + background-worker test.

### Frontend
- `frontend/paymentflow-web/src/app/core/realtime/payments-hub.service.ts` — SignalR client wrapper.

## Modified files (13)

### Backend
- `src/PaymentFlow.Domain/Entities/SecurityAuditEvent.cs` — added `PaymentCompleted` / `PaymentFailed` event types.
- `src/PaymentFlow.Infrastructure/DependencyInjection.cs` — register options/simulator/worker; accept SignalR `access_token` query param for `/hubs/*`.
- `src/PaymentFlow.Api/Program.cs` — `AddSignalR`, register notifier, CORS `AllowCredentials`, `MapHub`.
- `src/PaymentFlow.Api/Controllers/PaymentsController.cs` — `POST {id}/process` (policy `CanManagePayments`).
- `src/PaymentFlow.Api/appsettings.json` — new `Processing` section.
- `tests/PaymentFlow.Api.IntegrationTests/ApiFactory.cs` — pin worker off + zero latency for deterministic tests.

### Frontend
- `frontend/paymentflow-web/src/environments/environment.ts` — `hubUrl`.
- `frontend/paymentflow-web/package.json` / `package-lock.json` — add `@microsoft/signalr`.
- `frontend/paymentflow-web/src/app/core/services/payment.service.ts` — `process(id)`.
- `frontend/paymentflow-web/src/app/features/payments/payments.component.ts` / `.html` — live status via hub + "Process now" action.
- `frontend/paymentflow-web/src/app/features/dashboard/dashboard.component.ts` — live payment-activity feed.

## Verification status

Delivery environment has no .NET 10 SDK, so the backend was verified by static
review and pattern-matching (as in Phases 03–04). The Angular app compiles
(`ng build`; production font-inlining needs network the sandbox lacks). Please
run `dotnet test` on your machine.

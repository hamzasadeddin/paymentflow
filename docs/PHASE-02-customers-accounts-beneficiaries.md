# Phase 02: Customers, Accounts, and Beneficiaries

## Objective
Implement customer, payment account, and beneficiary management: domain entities, EF Core persistence, REST APIs with validation, masking, role-based authorization, pagination/filtering/sorting/search, and Angular management screens with the standard data-table pattern.

## Scope
- Individual and business customers (CRUD)
- Payment accounts under a customer: currency (ISO), available/ledger balance (decimal), status, limits, masked account numbers
- Beneficiaries: create, update, validate, submit-for-approval, approve/reject (lifecycle stub — full maker-checker arrives in Phase 04)
- Reusable paged-list contract (page, pageSize, sort, search, filters)
- Masking of account/IBAN numbers by default; unmasked only via explicit reveal-permitted endpoint for privileged roles
- Optimistic concurrency (RowVersion) on customers, accounts, beneficiaries
- Angular: customer list + detail (with accounts), beneficiary list + form
- Tests: domain (masking, transitions), application (validators), API integration (CRUD + authz + masking)
- Housekeeping: swallow `HostAbortedException` so migration commands print clean output

## Tasks
- [x] Domain: `Customer`, `PaymentAccount`, `Beneficiary`, enums, value helpers, `AuditableEntity` (RowVersion + timestamps)
- [x] Domain: account-number masking, beneficiary status transition rules + guards
- [x] Application: paging primitives (`PagedRequest`, `PagedResult`), `IApplicationDbContext`, DTOs
- [x] Application: CQRS for customers/accounts/beneficiaries + FluentValidation
- [x] Infrastructure: EF configurations, `IApplicationDbContext` binding, new migration
- [x] Api: `CustomersController`, `AccountsController`, `BeneficiariesController` with policies + status codes
- [x] Api: authorization policies per role; `HostAbortedException` handling in Program.cs
- [x] Angular: customers list/detail, beneficiaries list/form, shared data-table + status-chip + masked-value components, typed services
- [x] Tests across all three layers

## Key Files
- `src/PaymentFlow.Domain/Entities/{Customer,PaymentAccount,Beneficiary}.cs`
- `src/PaymentFlow.Domain/Common/AuditableEntity.cs`, `MaskingUtilities.cs`
- `src/PaymentFlow.Application/Common/Paging/*`, `Abstractions/IApplicationDbContext.cs`
- `src/PaymentFlow.Application/Features/{Customers,Accounts,Beneficiaries}/*`
- `src/PaymentFlow.Infrastructure/Persistence/Configurations/*`
- `src/PaymentFlow.Api/Controllers/{Customers,Accounts,Beneficiaries}Controller.cs`
- `src/PaymentFlow.Api/Extensions/AuthorizationPolicies.cs`
- `frontend/.../features/{customers,beneficiaries}/*`, `shared/*`

## API Endpoints
```
GET    /api/v1/customers                 (paged, filter, sort, search)   200
POST   /api/v1/customers                                                 201 | 400 | 409
GET    /api/v1/customers/{customerId}                                    200 | 404
PUT    /api/v1/customers/{customerId}                                    200 | 400 | 404 | 409
GET    /api/v1/customers/{customerId}/accounts                           200 | 404

GET    /api/v1/accounts/{accountId}                                      200 | 404
POST   /api/v1/customers/{customerId}/accounts                           201 | 400 | 404
GET    /api/v1/accounts/{accountId}/reveal-number  (privileged)          200 | 403 | 404

GET    /api/v1/beneficiaries             (paged, filter, sort, search)   200
POST   /api/v1/beneficiaries                                             201 | 400 | 409
GET    /api/v1/beneficiaries/{beneficiaryId}                             200 | 404
PUT    /api/v1/beneficiaries/{beneficiaryId}                             200 | 400 | 404 | 409
POST   /api/v1/beneficiaries/{beneficiaryId}/submit-for-approval         200 | 404 | 409
POST   /api/v1/beneficiaries/{beneficiaryId}/approve   (approver)        200 | 403 | 404 | 409
POST   /api/v1/beneficiaries/{beneficiaryId}/reject    (approver)        200 | 403 | 404 | 409
```

## Acceptance Criteria
- `dotnet build` and `dotnet test` green; new migration applies cleanly
- Customers list supports paging, `search`, status filter, and sort; response shape is the shared `PagedResult`
- Account numbers are masked (`****1234`) in all list/detail responses; only Administrator/ComplianceOfficer can reveal via the dedicated endpoint, and every reveal writes a security audit event
- Money fields are `decimal(19,4)`; currencies validated against an ISO allow-list
- Beneficiary `submit-for-approval` moves Draft→PendingApproval; approve/reject restricted to Payment Approver and enforce valid transitions (invalid transition → 409)
- Creating a customer/beneficiary is authorized for Operations Analyst + Administrator; Read-Only Auditor gets 403 on writes, 200 on reads
- Optimistic concurrency: a stale `rowVersion` on update returns 409
- Angular: customers and beneficiaries screens list data with loading/empty/error states, filtering, sorting, pagination; beneficiary form uses reactive validation; sensitive numbers masked by default

## Run and Test
```bash
dotnet ef migrations add CustomersAccountsBeneficiaries -p src/PaymentFlow.Infrastructure -s src/PaymentFlow.Api
dotnet run --project src/PaymentFlow.Api      # applies migration + seeds
dotnet test

cd frontend/paymentflow-web
npm install
npm start
npm test
```

## Notes
- Repository pattern is intentionally NOT introduced: `IApplicationDbContext` exposes `DbSet`s and EF Core already is the unit of work + repository. A repo layer would add indirection without value (revisit only if query logic needs reuse).
- Masking lives in the domain so it cannot be bypassed by a forgotten DTO mapping; the reveal endpoint is the single, audited exception.
- Beneficiary approval here is a lightweight status transition to unblock account/payment work; the configurable maker-checker engine (thresholds, dual approval, self-approval block) is Phase 04 and will supersede these two endpoints.
- Seed data extended with fictional customers, accounts, and beneficiaries for demoable screens.
- RowVersion (SQL `rowversion`) provides optimistic concurrency; SQLite tests map it to a byte[] shim so integration tests still exercise the 409 path.

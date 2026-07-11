# Phase 01: Foundation and Authentication

## Objective
Stand up the Clean Architecture solution, SQL Server persistence, JWT + refresh-token authentication with roles, structured logging, error handling, Swagger, Docker baseline, and the Angular shell with a working login flow.

## Scope
- Solution structure: Domain, Application, Infrastructure, Api + 3 test projects
- ASP.NET Core Identity (Guid keys), 5 roles, password policy, account lockout
- Login / refresh / logout / me endpoints with rotating, hashed refresh tokens
- Security audit events for login success/failure, refresh, logout
- Serilog + correlation IDs, RFC 7807 Problem Details, FluentValidation via MediatR pipeline
- Rate limiting on auth endpoints, health checks, Swagger, URL versioning (`/api/v1`)
- EF Core migrations + seed (roles, 5 demo users)
- Docker Compose (SQL Server + API), API Dockerfile
- Angular shell: login page, authenticated layout (sidebar + topbar), route guards, JWT/correlation/error interceptors, signal-based auth service
- Tests: domain unit, validator unit, API integration (health, login, refresh, me)

## Tasks
- [x] Create solution/projects with inward-pointing dependencies
- [x] Domain: `BaseEntity`, `RefreshToken`, `SecurityAuditEvent`, role constants
- [x] Application: Result pattern, MediatR commands (login/refresh/logout), validation + logging pipeline behaviors, `IIdentityService` abstraction
- [x] Infrastructure: `PaymentFlowDbContext` (IdentityDbContext), `IdentityService`, `JwtTokenService`, seeder, DI
- [x] Api: `AuthController`, correlation middleware, global exception handler, Problem Details, rate limiter, Swagger, health checks, CORS
- [x] Angular: login, shell layout, guards, interceptors, auth service + unit test
- [x] xUnit tests incl. integration tests on SQLite in-memory
- [ ] Run initial EF migration locally (`dotnet ef migrations add InitialCreate`)

## Key Files
- `src/PaymentFlow.Api/Program.cs` – composition root, middleware pipeline
- `src/PaymentFlow.Application/Features/Auth/*` – CQRS commands + validators
- `src/PaymentFlow.Infrastructure/Identity/IdentityService.cs` – login/refresh/logout + audit
- `src/PaymentFlow.Infrastructure/Persistence/DatabaseSeeder.cs` – roles + demo users
- `frontend/paymentflow-web/src/app/core/**` – auth service, interceptors, guards
- `docker-compose.yml`, `.env.example`

## API Endpoints
```
POST /api/v1/auth/login      200 | 400 | 401 | 429
POST /api/v1/auth/refresh    200 | 401 | 429
POST /api/v1/auth/logout     204 (auth required)
GET  /api/v1/auth/me         200 | 401
GET  /health                 200
```

## Acceptance Criteria
- `dotnet build` and `dotnet test` succeed (3 test projects green)
- API starts, applies migrations, seeds roles + demo users
- Login with `admin@paymentflow.local` returns access + refresh tokens; wrong password returns 401 Problem Details; 5 consecutive failures lock the account for 15 minutes
- Refresh rotates the token; a reused (revoked) refresh token is rejected with 401
- Every response carries `X-Correlation-Id`; all errors are RFC 7807
- Swagger UI available at `/swagger` in Development
- Angular: unauthenticated users are redirected to `/login`; successful login lands on the dashboard shell; logout clears the session

## Run and Test
```bash
cp .env.example .env                       # then edit secrets
docker compose up -d sqlserver
dotnet tool install -g dotnet-ef           # once
dotnet ef migrations add InitialCreate -p src/PaymentFlow.Infrastructure -s src/PaymentFlow.Api
dotnet run --project src/PaymentFlow.Api   # http://localhost:5080/swagger
dotnet test

cd frontend/paymentflow-web
npm install
npm start                                  # http://localhost:4200
npm test
```

## Notes
- Result pattern: expected business failures (bad credentials, lockout) flow as `Result` and map to Problem Details; exceptions are reserved for the unexpected.
- Refresh tokens are stored as SHA-256 hashes and rotated on every refresh; replay of a rotated token is rejected.
- MediatR pipeline behaviors implement the decorator concern (validation, logging) without touching handlers.
- Dev-only secrets live in `appsettings.Development.json`, clearly labeled; production values must come from environment variables / user secrets.
- Integration tests swap SQL Server for SQLite in-memory via a custom `WebApplicationFactory`.
- Next phases depend on: authenticated pipeline, seeding infrastructure, Angular shell navigation.

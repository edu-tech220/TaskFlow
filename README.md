# TaskFlow API

[![CI](https://github.com/edu-tech220/TaskFlow/actions/workflows/ci.yml/badge.svg)](https://github.com/edu-tech220/TaskFlow/actions/workflows/ci.yml)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![SQL Server](https://img.shields.io/badge/SQL%20Server-EF%20Core%2010-CC2927)
![License](https://img.shields.io/badge/license-MIT-green)

A REST API for **daily habit tracking**: users schedule recurring activities, record what they
completed each day, plan rest days within a monthly limit, and get a calendar that summarizes how
each day went.

The interesting part is the domain: schedules are **versioned** so changing a routine never rewrites
history, "pending" is **derived instead of stored**, and the monthly calendar is computed with a
**fixed number of SQL queries** regardless of how many activities exist.

**Frontend:** [edu-tech220/TaskFlow.Web](https://github.com/edu-tech220/TaskFlow.Web) (ASP.NET Core MVC).

![Dashboard (TaskFlow.Web)](https://raw.githubusercontent.com/edu-tech220/TaskFlow.Web/main/docs/images/dashboard.png)

## Highlights

- **Versioned schedules.** Changing an activity's days or hours closes the current version and
  starts a new one tomorrow; today and the past never change. A version that starts today or later
  and has no records is edited in place.
- **Derived state.** There is no `IsActive` column and no "pending" record: both are computed from
  schedule versions and the absence of a record.
- **Day status rules** evaluated in order (first match wins):
  `Future → RestDay → NoActivities → Completed → InProgress → Failed → Partial`.
- **Calendar without N+1.** A month is resolved with 4 queries (schedules, records, rest days,
  limit) using `AsNoTracking` projections; everything else is computed in memory by pure functions.
- **Per-user data isolation.** Every query is scoped to the authenticated user; requesting another
  user's resource returns 404.
- **Testable time.** "Today" is computed in a configurable time zone through `TimeProvider`, so
  tests control the clock.

## Tech stack

| Area | Technology |
|---|---|
| API | ASP.NET Core 10 Web API, OpenAPI + Swagger UI |
| Data | Entity Framework Core 10 (Code First, Fluent API, migrations), SQL Server |
| Security | JWT Bearer, `PasswordHasher` (PBKDF2), role-based authorization, rate limiting, CORS |
| Errors | `ProblemDetails` (RFC 9457) with stable error codes, global exception handler |
| Tests | xUnit, `WebApplicationFactory` against a real SQL Server, Postman/Newman collection |
| CI | GitHub Actions (build + 51 tests against a SQL Server container) |

## Getting started

Requirements: .NET SDK 10, SQL Server (Express works) at `.\SQLEXPRESS` with Windows authentication,
and the EF Core CLI (`dotnet tool install --global dotnet-ef`).

```powershell
# 1. JWT signing key (once per machine; never committed)
cd TaskFlow
dotnet user-secrets set "Jwt:Key" "<a-random-key-with-32+-characters>"

# 2. Create the database from the migrations
dotnet ef database update

# 3. Run the API (Swagger UI at https://localhost:7256/swagger)
dotnet run --launch-profile https
```

In Visual Studio pick the **https** launch profile (not IIS Express): the frontend expects the API at
`https://localhost:7256`.

## Configuration

| Key | Default | Purpose |
|---|---|---|
| `ConnectionStrings:TaskFlow` | `appsettings.Development.json` | Database connection |
| `Jwt:Key` | **User Secrets** | Signing key (32+ characters) |
| `Jwt:Issuer` / `Jwt:Audience` | `TaskFlow` | Validated on every token |
| `Jwt:ExpiresMinutes` | `480` | Token lifetime |
| `Clock:TimeZoneId` | `America/Mexico_City` | Time zone used to compute "today" |
| `RestDays:DefaultMonthlyLimit` | `4` | Rest-day limit for months without a custom one |
| `Cors:AllowedOrigins` | `[]` | Allowed browser origins |
| `RateLimiting:PermitLimitPerMinute` | `300` | Requests per IP per minute |
| `RateLimiting:AuthPermitLimitPerMinute` | `10` | Login/register attempts per IP per minute |

The development connection string uses Windows authentication (no password), so it lives in
`appsettings.Development.json`. If you use a SQL login, store it in User Secrets instead.

## Authentication

- Email + password login returns a **JWT** (HMAC-SHA256). Send it as `Authorization: Bearer <token>`.
- **Everything requires authentication** (global fallback policy) except `register`, `login` and the
  OpenAPI document.
- **The first registered user becomes Admin.** Admins can list users, change roles and deactivate
  accounts; a deactivated user's token stops working immediately.
- In Swagger: `POST /api/auth/login` → copy `token` → **Authorize**.

## API

| Method | Route | Notes |
|---|---|---|
| POST | `/api/auth/register` | Public. 201 + `{ token, expiresAt, user }` |
| POST | `/api/auth/login` | Public. 401 on invalid credentials |
| GET/PUT | `/api/auth/me` | Own profile |
| PUT | `/api/auth/me/password` | `{ currentPassword, newPassword }` |
| GET | `/api/users` | Admin only |
| PUT | `/api/users/{id}` | Admin only: `{ role, isActive }` (not on yourself) |
| GET | `/api/activities` | Activities with current schedule and history |
| GET | `/api/activities/{id}` | |
| POST | `/api/activities` | Creates the activity and its first schedule version (201 + Location) |
| PUT | `/api/activities/{id}` | Name/description; optional `schedule` creates a new version from tomorrow |
| PATCH | `/api/activities/{id}/status` | `false` closes today; `true` reactivates from tomorrow |
| DELETE | `/api/activities/{id}` | 409 if it has records (deactivate instead) |
| GET | `/api/rest-days?year=&month=` | Month summary: limit, used, remaining, days |
| POST | `/api/rest-days` | Today or future only; enforces the monthly limit (409) |
| DELETE | `/api/rest-days/{date}` | Today or future only |
| GET/PUT | `/api/rest-day-limits/{year}/{month}` | 0–10; cannot go below days already used (409) |
| GET | `/api/daily-records?date=` or `?from=&to=` | |
| PUT | `/api/daily-records/{activityId}/{date}` | Upsert: 201 on create, 200 on update |
| DELETE | `/api/daily-records/{activityId}/{date}` | Undo a record |
| GET | `/api/calendar?year=&month=` | Status of every day (4 SQL queries) |
| GET | `/api/calendar/days/{date}` | A single day |

All errors are `application/problem+json` with a stable `code` extension
(`ActivityHasRecords`, `MonthlyRestLimitReached`, `ScheduleMustStartInFuture`, ...).

## Tests

```powershell
dotnet test    # 51 tests; creates and drops the TaskFlow_Tests database
```

- **Unit tests:** day-status rules and their precedence, time-zone handling, schedule rules.
- **Integration tests:** the full API in memory with `WebApplicationFactory` against a real SQL
  Server and a controllable clock: authentication, per-user isolation, admin operations, expired
  tokens, schedule versioning, rest-day limits, daily records and calendar statuses.
- **Postman:** `postman/` contains a collection (50 requests, 116 assertions) runnable with Newman:

```powershell
npx newman run postman/TaskFlow.postman_collection.json -e postman/TaskFlow.Local.postman_environment.json
```

Integration tests use `.\SQLEXPRESS` locally or the `TASKFLOW_TEST_CONNECTION` environment variable
(that is how CI runs them against a SQL Server container).

## Migrations

```powershell
cd TaskFlow
dotnet ef migrations add <Name> -o Data/Migrations
dotnet ef database update
# Roll everything back and remove the last migration:
dotnet ef database update 0
dotnet ef migrations remove
```

## Project structure

```
TaskFlow/
  Common/        Result/Error, AppClock (time zone), options, global error handling, DI extensions
  Controllers/   Thin controllers: HTTP <-> service
  Data/          TaskFlowDbContext, Configurations/ (Fluent API), Migrations/
  DTOs/          Request/response contracts (entities are never exposed)
  Entities/      EF Core model
  Enums/         Persisted status and calculated statuses
  Services/      Business rules; DayStatusCalculator and ScheduleRules are pure functions
tests/TaskFlow.Tests/
  Unit/          Pure rules
  Integration/   API + SQL Server
postman/         Collection and environment
```

## License

[MIT](LICENSE)

# CEMS — Center Educational Management System

A full-stack management system for a multi-branch educational center:
branches and rooms, teachers, students and guardians, courses tied to
curricula, scheduled sessions, attendance, exams and grades, payments, and
payroll.

Built solo, backend and frontend fully separate with no shared code,
communicating only over HTTP — a deliberate boundary, not a limitation of
the setup.

## Screenshots

<table>
<tr>
<td width="50%"><img src="docs/screenshots/dashboard.png" alt="Owner dashboard"/><br/><sub>Owner dashboard — revenue, attendance, and enrollment at a glance</sub></td>
<td width="50%"><img src="docs/screenshots/students.png" alt="Students list"/><br/><sub>Students — branch-scoped roster</sub></td>
</tr>
<tr>
<td width="50%"><img src="docs/screenshots/teachers.png" alt="Teacher profile"/><br/><sub>Teacher profile — branches, availability, declared qualifications</sub></td>
<td width="50%"><img src="docs/screenshots/scheduling.png" alt="Scheduling"/><br/><sub>Scheduling — sessions with a live teacher substitution</sub></td>
</tr>
<tr>
<td width="50%"><img src="docs/screenshots/payroll.png" alt="Payroll"/><br/><sub>Payroll — generated runs, draft and paid</sub></td>
<td width="50%"><img src="docs/screenshots/analytics.png" alt="Analytics"/><br/><sub>Analytics — revenue, attendance, and teacher utilization</sub></td>
</tr>
</table>

## Highlights

- **Role-based access control enforced server-side.** Every non-Owner
  request is scoped to the caller's own branch(es) at the API/query level —
  never just a hidden button in the UI.
- **Clean Architecture + CQRS.** `Domain` → `Application` → `Infrastructure`
  → `Api`, with MediatR commands/queries and FluentValidation on every
  input.
- **Real scheduling logic.** Room double-booking, teacher double-booking,
  and declared-availability conflicts are all checked automatically, with
  an audited override path for Owner/BranchManager only. Sessions can be
  reassigned to a substitute teacher in place, running the same conflict
  checks as creating one.
- **Branch transfers with history.** Moving a student between branches
  updates access immediately and keeps an auditable record of every move.
- **Billing and payroll that actually compute.** Invoices track
  paid/partial/overdue automatically from recorded payments; payroll runs
  are computed from real session data across four pay structures (hourly,
  per-session, fixed, and percentage-of-revenue).
- **PDF/Excel exports** for report cards, pay stubs, and the analytics
  dashboard (QuestPDF, ClosedXML).
- **Tested where it matters.** Backend handler tests run against a real EF
  Core context (SQLite in-memory, not mocks) so query-translation bugs
  can't hide behind a mock; frontend tests cover the client-side logic that
  can break silently (schemas, datetime conversions, the attendance-window
  rule).

## Tech stack

**Backend** (`Backend/`)
- ASP.NET Core Web API (.NET 9)
- EF Core + PostgreSQL (Npgsql provider)
- ASP.NET Identity + JWT authentication
- Clean Architecture: `Domain` → `Application` → `Infrastructure` → `Api`
- MediatR (CQRS), FluentValidation
- QuestPDF (report card / dashboard PDF generation)
- ClosedXML (dashboard Excel export)
- xUnit (`Backend/tests/CEMS.Application.Tests`) — handler unit tests
  against a real EF Core `ApplicationDbContext` backed by SQLite in-memory

**Frontend** (`Frontend/`)
- React 19 + Vite + TypeScript
- Tailwind CSS v4 + hand-built primitives in `shared/ui/` (no component
  library — Button, Input, Select, Modal, PageHeader, StatCard, SearchInput)
- React Hook Form + Zod
- TanStack Query + Context for auth, axios client, React Router
- Vitest + React Testing Library

## Roles

| Role | Scope |
|---|---|
| Owner | Org-wide, full visibility and administration |
| BranchManager | One branch |
| Teacher | One or more branches (floating) |
| FrontDesk | One branch |

CEMS is a staff-run tool with no parent-facing login or self-service
portal. Guardians (name, phone, email, relationship, primary-contact flag)
are plain contact records staff manage on a student, with no account
attached.

## Architecture notes

- Domain entities are plain POCOs with no framework dependencies. Identity
  (`ApplicationUser`) lives in `Infrastructure`; domain entities that
  reference a user (e.g. branch scoping) store a plain `Guid UserId`, not a
  navigation property, keeping `Domain` independent of `Infrastructure`.
- `Application` is organized into feature folders that mirror the domain
  modules (Branches, Users, Students, Teachers, Courses, Scheduling,
  Attendance, Exams, Payments, Payroll, Analytics); the frontend's
  `features/` folders mirror the same breakdown 1:1.
- All `DateTime` values are stored and compared as UTC. A global EF Core
  value converter enforces `DateTimeKind.Utc` on every `DateTime` property,
  since Npgsql rejects `Kind=Unspecified` for `timestamptz` columns.
- Teacher availability and session scheduling compare UTC day-of-week /
  time-of-day directly, with no per-branch timezone handling — correct for
  a single-timezone deployment; a multi-region version would give each
  `Branch` an IANA timezone.
- CORS is config-driven (`Cors:AllowedOrigins` in `appsettings.json`), and
  the frontend authenticates via a Bearer token rather than cookies, so no
  cross-origin credential exposure is needed.

## Getting started

Requires a local PostgreSQL instance and a `cems` database. The connection
string and JWT signing key are never committed — they're set via .NET's
[Secret Manager](https://learn.microsoft.com/aspnet/core/security/app-secrets),
which stores them outside the repo entirely. Run from `Backend/src/CEMS.Api`:

```bash
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=cems;Username=postgres;Password=YOUR_PASSWORD"
dotnet user-secrets set "Jwt:Key" "<a long random string, at least 32 characters>"
```

`Program.cs` fails fast at startup if either is missing or the JWT key is
too short. In any other environment, the same values are set as real
environment variables instead (`ConnectionStrings__Default`, `Jwt__Key`) —
see "Hosting" below.

Apply migrations from `Backend/`:

```bash
dotnet ef database update --project src/CEMS.Infrastructure --startup-project src/CEMS.Api
```

There's no self-registration endpoint — every account is created by an
Owner or staff member via `POST /api/users/staff`. The one exception is the
very first account: `POST /api/auth/bootstrap-owner` is anonymous but
self-disables the instant any user exists, so it can never be used as a
general signup path.

```bash
curl -X POST https://localhost:7099/api/auth/bootstrap-owner \
  -H "Content-Type: application/json" \
  -d '{"email":"you@example.com","password":"YourPassword123","fullName":"Your Name","phoneNumber":"0100000000"}'
```

### Demo data

`scripts/seed-demo-data.ps1` populates a fresh, empty `cems` database with a
realistic dataset — two branches, staff at every role, teachers with
availability, students with linked guardians, enrollments, scheduled
sessions, attendance, a graded exam, invoices in every payment state, and an
approved payroll run. It drives the real running API end to end (not a raw
SQL dump), so every row passes through actual password hashing, RBAC,
scheduling conflict checks, and payroll computation. Not idempotent — it's
meant to run once against a clean database.

```powershell
# 1. Start the API first (see above for env vars)
dotnet run --project Backend/src/CEMS.Api
# 2. In another terminal, from the repo root:
./scripts/seed-demo-data.ps1
```

Prints every demo account's email at the end (all passwords: `DemoPass123`).

## Testing

```bash
dotnet test Backend/tests/CEMS.Application.Tests/CEMS.Application.Tests.csproj
```

Backend tests target the handlers with the trickiest business rules —
attendance-window enforcement, branch-transfer access checks, payroll
computation across all four pay types, admin-assisted password reset across
the Teacher/BranchManager branch-scoping boundary, and teacher
substitution's conflict/override rules. Each test runs against a real
`ApplicationDbContext` on a fresh SQLite in-memory database rather than a
mocked context, so EF query-translation issues can't hide behind a mock.

```bash
cd Frontend && npm test
```

Most business logic lives server-side by design, so the frontend suite
targets client-side logic that's intricate enough to break silently: the
datetime-local ⇄ UTC-ISO conversions scheduling forms depend on, the
attendance-window rule (extracted for standalone testing), pay-rate and
student-creation form schemas, and the shared search component.

## Local backups

Not hosted anywhere yet, so there's no managed provider taking automatic
backups. Until then:

```powershell
./scripts/backup-database.ps1
```

Writes a Postgres custom-format (`-Fc`) dump to `Backend/backups/`
(gitignored). To restore:

```powershell
./scripts/restore-database.ps1 -BackupFile Backend/backups/cems_2026-09-15_162557.dump
```

Restores into a separate `<database>_restore_test` database by default, not
the real one — pass `-Overwrite` to restore onto the real database
directly. Both scripts have been run end-to-end and verified with matching
row counts across every table.

## What's not included

This is architecturally sound for one real educational center (staff-run,
no self-service parent/student portal), but it hasn't been hardened for
production use. Notable gaps:

- **Single timezone.** Session scheduling assumes one timezone across all
  branches.
- **No refresh tokens.** JWTs last 6 hours; after that it's a hard logout,
  not a silent renewal.
- **No email**, by design — no password-reset mail, no invoice-due
  reminders. Password resets are admin-assisted instead.
- **No containerization**, by design — this deploys via whatever the host
  builds natively (Railway's Nixpacks, Azure App Service) rather than a
  Dockerfile.
- **No CI/CD pipeline yet.**
- Test coverage is a starting set on both sides, not exhaustive.

Turning this into a true multi-tenant product (many unrelated centers, not
one center with several branches) would need a separate
organization/tenant concept, per-tenant data isolation, and billing — a
materially bigger redesign, not assumed here.

## Hosting

Not yet deployed. No Dockerfile, by design — this deploys via whatever the
host builds natively from source. The intended path for this stack (ASP.NET
Core 9 API + PostgreSQL + Vite/React SPA):

- **Database**: [Neon](https://neon.tech) (managed Postgres).
- **API**: Railway (Nixpacks, no Dockerfile needed) or Azure App Service —
  either reads the same `ConnectionStrings__Default` / `Jwt__Key`
  environment variables `Program.cs` already reads locally, so no code
  change is needed to go from local to hosted.
- **Frontend**: the Vite static build (`npm run build`) to Vercel or
  Netlify.

## License

MIT — see [LICENSE](LICENSE).

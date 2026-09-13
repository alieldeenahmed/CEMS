# CEMS — Center Educational Management System

A multi-branch educational center management system: branches, rooms, teachers,
students, parents/guardians, courses tied to curricula, scheduled sessions,
attendance, exams/grades, payments, and payroll.

Backend and frontend are fully separate — no shared code — and communicate only
over HTTP.

## Tech stack

**Backend** (`Backend/`)
- ASP.NET Core Web API (.NET 9)
- EF Core + PostgreSQL (Npgsql provider)
- ASP.NET Identity + JWT authentication
- Clean Architecture: `Domain` → `Application` → `Infrastructure` → `Api`
- MediatR (CQRS), FluentValidation, AutoMapper
- QuestPDF (report card / dashboard PDF generation)
- ClosedXML (dashboard Excel export)

**Frontend** (`Frontend/`) — not started yet
- React + Vite + TypeScript
- Tailwind CSS + shadcn/ui
- React Hook Form + Zod
- TanStack Query + Context for auth

## Roles

| Role | Scope |
|---|---|
| Owner | Org-wide, full visibility and administration |
| BranchManager | One branch |
| Teacher | One or more branches (floating) |
| FrontDesk | One branch |
| Parent | Their own children only, regardless of branch |

Every non-Owner request is scoped to the caller's branch(es) at the API/query
level — never just hidden in the UI.

## Architecture notes

- Domain entities are plain POCOs with zero framework dependencies. Identity
  (`ApplicationUser`) lives in `Infrastructure` since it depends on
  `Microsoft.AspNetCore.Identity`; domain entities that need to reference a
  user (e.g. branch scoping) store a plain `Guid UserId`, not a navigation
  property, to keep `Domain` independent of `Infrastructure`.
- `Application` is organized into feature folders that mirror the domain
  modules (Branches, Users, Students, Teachers, Courses, Scheduling,
  Attendance, Exams, Payments, Payroll, Analytics). The frontend's
  `features/` folders will mirror the same breakdown 1:1.
- All `DateTime` values in the model represent UTC instants (`CourseSession`
  start/end). `ApplicationDbContext` applies a global value converter that
  forces `DateTimeKind.Utc` on every `DateTime` property — Npgsql rejects
  `Kind=Unspecified` for `timestamptz` columns, and JSON-deserialized
  timestamps come back as `Unspecified` unless the client includes a `Z`
  suffix. This converter means the API keeps working even if a client
  forgets it, rather than throwing a 500.
- All times in `TeacherAvailability` and session scheduling are compared as
  literal UTC day-of-week/time-of-day, with no branch-timezone handling.
  Fine for a single-timezone portfolio deployment; a real multi-region
  system would need each `Branch` to carry an IANA timezone.
- CORS is config-driven, not hardcoded: `Cors:AllowedOrigins` in
  `appsettings.json` (defaults to `http://localhost:5173`, Vite's dev port).
  `AllowAnyHeader`/`AllowAnyMethod` but no `AllowCredentials` — the frontend
  authenticates via a Bearer token in the `Authorization` header, not
  cookies, so no cross-origin credential exposure is needed. Verified live:
  a disallowed origin gets a 204 preflight response with no
  `Access-Control-Allow-Origin` header at all (browser blocks it), while an
  allowed origin gets the header echoed back correctly.

## Local setup

Requires a local PostgreSQL instance and a `cems` database. The connection
string is never committed — set it as a user environment variable instead:

```powershell
[System.Environment]::SetEnvironmentVariable("ConnectionStrings__Default", "Host=localhost;Port=5432;Database=cems;Username=postgres;Password=YOUR_PASSWORD", "User")
```

The JWT signing key is set the same way — it's a secret, so it's never
committed either. Any random string works locally:

```powershell
[System.Environment]::SetEnvironmentVariable("Jwt__Key", "<a long random string>", "User")
```

Restart your terminal/IDE afterward, then apply migrations from `Backend/`:

```bash
dotnet ef database update --project src/CEMS.Infrastructure --startup-project src/CEMS.Api
```

**If `dotnet ef` fails with an assembly-load / "Application Control policy"
error**: this is Windows Smart App Control blocking `dotnet-ef`'s reflection
load of a freshly-built local DLL, not a code problem. `CEMS.Infrastructure`
has an `ApplicationDbContextFactory` (`IDesignTimeDbContextFactory`) so `dotnet
ef migrations add` can be run with just `--project src/CEMS.Infrastructure`
(no `--startup-project`), which avoids loading `CEMS.Api.dll` — but if Smart
App Control still blocks it, run the app once instead: temporarily add
`app.Services.CreateScope().ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();`
after `var app = builder.Build();` in `Program.cs`, `dotnet run` once, then
remove it. Normal process execution isn't affected by this, only `dotnet-ef`'s
reflection-based assembly loading is.

There's currently no seeded Owner account and no self-registration path to
one (self-registration always creates a `Parent`, and creating an `Owner`
isn't exposed through any endpoint by design). To bootstrap the very first
Owner locally: register a normal account through `/api/auth/register`, then
manually insert its role in the database —

```sql
INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
SELECT "Id", '11111111-1111-1111-1111-111111111111' FROM "AspNetUsers" WHERE "Email" = 'you@example.com';
```

— then log in again to get a token carrying the `Owner` role. Every other
account (BranchManager/Teacher/FrontDesk) can be created normally afterward
via `POST /api/users/staff`.

### Demo data

`scripts/seed-demo-data.ps1` populates a fresh, empty `cems` database with a
realistic dataset — 2 branches, staff at every role, teachers with
availability, parents with linked students, enrollments, scheduled sessions,
attendance, a graded exam, invoices in every payment state (paid/partial/
overdue), and an approved payroll run. It's a script that calls the real
running API end to end, not a raw SQL dump — every row passes through actual
password hashing, RBAC, scheduling conflict checks, and payroll computation,
the same way this whole backend has been verified throughout development.
It is **not idempotent** — it's meant to run once against a clean database,
not as a repeatable fixture loader.

```powershell
# 1. Start the API first (see above for env vars)
dotnet run --project Backend/src/CEMS.Api
# 2. In another terminal, from the repo root:
./scripts/seed-demo-data.ps1
```

Prints every demo account's email at the end (all passwords: `DemoPass123`).

## Progress

- [x] Backend solution scaffold (4 Clean Architecture projects)
- [x] Core NuGet packages wired up
- [x] `Branch` / `Room` domain entities
- [x] `ApplicationUser` (Identity) + `UserBranchAssignment`
- [x] `ApplicationDbContext`
- [x] DI wiring (Program.cs, connection string) + first migration
- [x] Role seeding (Owner, BranchManager, Teacher, FrontDesk, Parent)
- [x] Auth endpoints (register/login, JWT issuing)
- [x] Branch/Room CRUD endpoints, branch-scoped RBAC
- [x] Admin endpoint for creating staff accounts (Owner creates any staff
      role; BranchManager creates Teacher/FrontDesk for their own branch only)
- [x] Student Management: Student/Guardian CRUD, student-guardian linking,
      branch-scoped and Parent-scoped RBAC (self-registration auto-creates a
      Guardian record; branch transfer with history is deferred — see below)
- [x] Teacher Management: Teacher profile CRUD (Owner-only, pay rate is
      sensitive), floating branch assignment, self-managed availability
      windows (teacher/BranchManager/Owner, all branch-scoped)
- [x] Course & Curriculum Management: Curriculum/Subject (org-wide catalog,
      viewable by anyone, Owner-only to edit), Course (branch-scoped,
      Owner/BranchManager manage it), CourseEnrollment (soft-drop, enforces
      student and course share a branch; Parent can view their child's
      enrollments)
- [x] Scheduling & Room Booking: `CourseSession` with conflict-checking
      against room double-booking, teacher double-booking, and teacher
      declared availability; Owner/BranchManager can override a detected
      conflict with a required reason (audit-logged), FrontDesk cannot;
      structural checks (room/course branch match, teacher assigned to
      branch) are never overridable. Cancelling a session can link to a
      makeup session. Enrollment waitlisting (`CourseEnrollment.Position`)
      derives capacity from the room of the course's earliest session;
      promotion is manual (no auto-promotion)
- [x] Attendance: `SessionAttendance` marked by the assigned teacher (self)
      or Owner/BranchManager (branch-scoped admin correction) — never
      FrontDesk. Roster view shows every actively-enrolled student
      defaulting to `Unmarked` (no background job for no-show flagging, per
      the earlier decision — staff review is manual). History view is
      Parent-scoped like enrollments/guardians.
- [x] Exams & Grades: `Exam` (per course) and `Grade` (upsert, like
      Attendance) managed by Owner/BranchManager (branch-scoped) or Teacher
      — but only for courses they actually teach, checked live against
      `CourseSession.TeacherId`, not just branch membership. FrontDesk views
      only. Exam roster mirrors the Attendance pattern (every enrolled
      student, `null` score if ungraded); student grade history is
      staff-admin/Parent-scoped, deliberately excluding Teacher (a teacher
      shouldn't see a student's grades from courses they don't teach).
      Report cards render as real PDFs via QuestPDF (`IReportCardGenerator`
      abstraction, same pattern as `IIdentityService`), grouped by course.
- [x] Payments & Fees: `Package` (course-scoped billing catalog),
      `Invoice` (amount immutable after creation — set server-side from
      `Package.Price` if package-based, client-supplied for ad-hoc charges;
      `Overdue` is a computed DTO field, never stored, no background job),
      `Payment` (recording one recalculates the invoice's status
      Pending→PartiallyPaid→Paid automatically). FrontDesk gets real
      write access here (invoicing/collecting payment is their actual job,
      unlike the view-only role they had in Attendance/Exams); cancelling
      an invoice is Owner/BranchManager-only and blocked once fully paid.
      Dedicated outstanding-balance endpoint sums unpaid amounts across all
      non-cancelled invoices for a student, Parent-visible for their own
      child.
- [x] Payroll: `PayrollRun` (Draft→Approved→Paid) auto-computed from every
      non-cancelled `CourseSession` in the period, priced by `PayType`
      (Hourly × duration, or flat PerSession) — a center-cancelled session
      isn't paid, but its linked makeup session is a normal session and is
      paid like any other, per the earlier no-show-vs-cancellation
      decision. `PayrollLineItem` traces each session's contribution.
      Entirely Owner-only (generation, approval, payout) since it reveals
      actual computed compensation, same sensitivity level as `PayRate`
      itself — except a teacher can view their own runs, self-service like
      `my-profile`/`my-schedule`. Guards against generating two runs with
      overlapping periods for the same teacher.
- [x] Analytics Dashboard: pure read/aggregation layer, no new entities —
      cross-branch revenue (invoiced/collected/outstanding), teacher
      utilization (scheduled vs. available hours, availability approximated
      as weekly-pattern × number of weeks in the period), attendance trends
      (rate over stored records — "Unmarked" is only ever a computed
      roster-view default, never a stored row, so it's deliberately left
      out of trends), and an enrollment funnel (total → any enrollment →
      active enrollment). Owner queries org-wide or any branch;
      BranchManager must supply their own branch (`BranchId` is required,
      not optional, for non-Owner — omitting it does not fall back to an
      implicit org-wide view). Exports to real PDF (QuestPDF) and Excel
      (ClosedXML, genuinely MIT-licensed). All four metrics and both export
      formats verified against live data with hand-checked arithmetic.
- [ ] Frontend scaffold — **this is the last thing before the backend
      (11/11 modules) is functionally complete**
- [ ] Student branch transfer with history (`StudentBranchHistory`) —
      deliberately deferred until it's the thing being built, not bare CRUD
- [ ] `TeacherSubject` (which subjects a teacher teaches) — still deferred;
      no module has needed it yet

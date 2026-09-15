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
- MediatR (CQRS), FluentValidation
- QuestPDF (report card / dashboard PDF generation)
- ClosedXML (dashboard Excel export)

**Frontend** (`Frontend/`)
- React 19 + Vite + TypeScript
- Tailwind CSS v4 + hand-built primitives in `shared/ui/` (no component
  library — Button, Input, Select, Modal, PageHeader, StatCard, SearchInput)
- React Hook Form + Zod
- TanStack Query + Context for auth, axios client, React Router

## Roles

| Role | Scope |
|---|---|
| Owner | Org-wide, full visibility and administration |
| BranchManager | One branch |
| Teacher | One or more branches (floating) |
| FrontDesk | One branch |

CEMS has no parent-facing login or self-service portal — it's a tool the
school's staff run the business on, not a consumer app. Guardians (name,
phone, email, relationship, primary-contact flag) are plain contact records
staff manage on a student, with no account or login attached.

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
  `features/` folders mirror the same breakdown 1:1.
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

There's no general self-registration endpoint — every account is created by
an Owner or staff member via `POST /api/users/staff`, matching a tool the
school's staff run rather than a product people sign up for. That leaves a
chicken-and-egg problem for the very first account, since creating staff
already requires an authenticated Owner token. `POST /api/auth/bootstrap-owner`
solves it: it's anonymous, but `BootstrapOwnerCommandHandler` checks whether
any user exists at all and rejects with 403 the instant one does, so it
self-disables after the very first call and can never be used as a general
signup path.

```bash
curl -X POST https://localhost:7099/api/auth/bootstrap-owner \
  -H "Content-Type: application/json" \
  -d '{"email":"you@example.com","password":"YourPassword123","fullName":"Your Name","phoneNumber":"0100000000"}'
```

The response is a normal `AuthResultDto` with a ready-to-use token carrying
the `Owner` role. Every other account (BranchManager/Teacher/FrontDesk) is
created afterward via `POST /api/users/staff`.

### Demo data

`scripts/seed-demo-data.ps1` populates a fresh, empty `cems` database with a
realistic dataset — 2 branches, staff at every role, teachers with
availability, students with linked guardian contacts, enrollments, scheduled
sessions, attendance, a graded exam, invoices in every payment state (paid/
partial/overdue), and an approved payroll run. It's a script that calls the real
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
- [x] Role seeding (Owner, BranchManager, Teacher, FrontDesk)
- [x] Auth endpoints (login, JWT issuing; `bootstrap-owner` for the very
      first account only — see "Local setup" below)
- [x] Branch/Room CRUD endpoints, branch-scoped RBAC
- [x] Admin endpoint for creating staff accounts (Owner creates any staff
      role; BranchManager creates Teacher/FrontDesk for their own branch
      only) and admin-assisted password reset with the same boundary
      (`POST /users/staff/{id}/reset-password`, no email involved — an
      admin sets the password directly and relays it). BranchManager also
      gets a branch-scoped staff list (`GET /users/staff/my-branch`) —
      Teacher and FrontDesk at their own branch only, never another
      BranchManager, Owner, or another branch's staff. A Teacher's branch
      for all three of these comes from their Teacher profile
      (`TeacherBranch`), not `UserBranchAssignments` — that table is only
      populated for FrontDesk/BranchManager at account creation, so it's
      always empty for a Teacher, a subtlety caught and fixed in each case
      before shipping.
- [x] Student Management: Student/Guardian CRUD, student-guardian linking,
      branch-scoped RBAC. Guardians are plain contact records (name/phone/
      email/relationship/primary-contact) with no login — CEMS has no
      parent-facing portal (branch transfer with history is deferred — see
      below)
- [x] Teacher Management: Teacher profile CRUD (Owner-only, pay rate is
      sensitive), floating branch assignment, self-managed availability
      windows (teacher/BranchManager/Owner, all branch-scoped)
- [x] Course & Curriculum Management: Curriculum/Subject (org-wide catalog,
      viewable by anyone, Owner-only to edit), Course (branch-scoped,
      Owner/BranchManager manage it), CourseEnrollment (soft-drop, enforces
      student and course share a branch)
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
      the earlier decision — staff review is manual).
- [x] Exams & Grades: `Exam` (per course) and `Grade` (upsert, like
      Attendance) managed by Owner/BranchManager (branch-scoped) or Teacher
      — but only for courses they actually teach, checked live against
      `CourseSession.TeacherId`, not just branch membership. FrontDesk views
      only. Exam roster mirrors the Attendance pattern (every enrolled
      student, `null` score if ungraded); student grade history is
      staff-only, deliberately excluding Teacher (a teacher shouldn't see a
      student's grades from courses they don't teach).
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
      non-cancelled invoices for a student.
- [x] Payroll: `PayrollRun` (Draft→Approved→Paid) auto-computed from every
      non-cancelled `CourseSession` in the period for Hourly (rate ×
      duration) or PerSession (flat per session) teachers — a
      center-cancelled session isn't paid, but its linked makeup session is
      a normal session and is paid like any other, per the earlier
      no-show-vs-cancellation decision. `PayrollLineItem` traces each
      session's contribution. Two more `PayType`s compute without any
      session loop at all, so they carry no line items: Fixed pays a flat
      `PayRate` for the whole period regardless of sessions held; Percentage
      pays `PayRate`% of whatever `Payment`s were actually recorded, in that
      period, against packages on courses the teacher teaches (not of
      sessions, and not of invoiced-but-uncollected amounts) — `PayRate` is
      validated ≤100 only when `PayType` is Percentage. Generation is
      Owner-only, same sensitivity level as `PayRate` itself, except a
      teacher can view their own runs self-service, like
      `my-profile`/`my-schedule`. Guards against generating two runs with
      overlapping periods for the same teacher. A parallel `StaffPayrollRun`
      model gives FrontDesk/BranchManager a flat, manually-entered (and
      Owner-editable while still Draft) payroll amount — they have no
      sessions to compute from. Every payroll run, teacher or staff, can be
      printed as a real PDF pay stub (`IPayStubGenerator`, QuestPDF).
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
- [x] Frontend: a page per backend module (Dashboard, Branches, Staff,
      Students, Teachers, Courses, Curricula, Scheduling, Attendance, Exams
      & Grades, Payments, Payroll, Analytics), all backed by the same
      branch-scoped RBAC as the API — a role that can't call an endpoint
      simply doesn't see the button/page for it, but the real enforcement is
      always server-side. Dashboard home is role-personalized (Owner sees
      org-wide widgets; FrontDesk/Teacher get their own relevant overview,
      stacked if a user holds more than one role). Students, Teachers,
      Courses, Curricula, and Staff all have a client-side search bar.
      Clicking a student opens a dedicated detail page (enrollments,
      attendance, grades, and — Owner/BranchManager/FrontDesk only —
      guardians); a Teacher can open it too, but only for a student they
      actually teach, and only sees the slice of that history from their
      own courses, enforced in the query handler, not just the UI. A new
      student can't be created without a guardian attached at creation time
      (existing or new, atomic with the student row) — front desk used to
      be able to add one with no contact on file at all.
- [ ] Student branch transfer with history (`StudentBranchHistory`) —
      deliberately deferred until it's the thing being built, not bare CRUD
- [ ] `TeacherSubject` (which subjects a teacher teaches) — still deferred;
      no module has needed it yet

## Production readiness

This is architecturally sound for **one real educational center** (staff-run,
no self-service parent/student portal) — the RBAC scoping and business-rule
enforcement described above are real, not portfolio theater. It is **not**
ready to hand to a real center as-is. Checked, not guessed — concrete gaps,
in priority order:

**Would break in week one:**
- ~~No password reset, anywhere.~~ Fixed, admin-assisted (no email
  involved): `POST /api/users/staff/{id}/reset-password` sets a new
  password directly. Owner can reset anyone; BranchManager can reset a
  Teacher or FrontDesk at their own branch only — same boundary
  `CreateStaffUser` already enforces. A Teacher's branch for this check
  comes from their Teacher profile (`TeacherBranch`), not
  `UserBranchAssignments` (that table is only populated for
  FrontDesk/BranchManager at account creation, so it's always empty for a
  Teacher — caught and fixed before shipping). True self-service
  ("forgot password" via email) is still not built — it depends on the
  email gap below.
- ~~JWT access tokens expire after 60 minutes with no refresh token, so
  front desk got logged out mid-shift.~~ Fixed — `Jwt:ExpiryMinutes` is now
  360 (6h), long enough for a full shift; still no refresh token, so it's
  still a hard logout at that point, not a silent renewal.
- Zero automated tests, backend or frontend. Every behavior described in
  this README was verified by hand (curl + browser) during development —
  fine while building, not a safe way to change code once real data is in
  it.
- No backup/disaster-recovery story — it's whatever Postgres instance you
  point `ConnectionStrings__Default` at, with no documented restore path.

**Needed soon, not day-one:**
- No email at all — no password-reset mail, no invoice-due reminders,
  nothing. Front-desk staff will expect this quickly.
- Single-timezone only (see "Architecture notes" above) — fine for one
  region, breaks the moment a branch is elsewhere.
- Secrets (connection string, JWT signing key) are user-scoped environment
  variables — fine for local dev, not for a real deployment; needs a real
  secrets manager.
- `AutoMapper` is referenced in `CEMS.Application.csproj` but not used
  anywhere — every DTO is hand-constructed. Dead dependency, safe to drop.

**Can wait:** CI/CD, Docker packaging, and the two items already deferred
above (`StudentBranchHistory`, `TeacherSubject`).

Turning this into a real multi-tenant product (selling to many *unrelated*
centers, not just one center with several branches) would be a materially
bigger redesign — a separate `Organization`/tenant concept, per-tenant data
isolation, billing — not assumed here.

## Hosting

Not yet deployed anywhere; no Dockerfile, no CI/CD workflow, no hosting
config committed. For this stack (ASP.NET Core 9 API + PostgreSQL +
Vite/React SPA), the intended path is:

- **Database**: [Neon](https://neon.tech) (managed Postgres) — backups and
  patching stop being something to think about, generous free tier.
- **API**: Railway or Azure App Service (Linux). Railway is the faster,
  cheaper path to a first real deployment; Azure App Service is the more
  natural fit if this should also read as an Azure-flavored .NET deployment.
- **Frontend**: the Vite static build (`npm run build`) to Vercel or
  Netlify — trivial either way.

Docker packaging and a basic CI workflow (build + the tests that don't yet
exist) should land before a real center is using this, since every
deployment right now would be a manual one.

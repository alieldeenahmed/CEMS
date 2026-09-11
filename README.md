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
- [ ] Frontend scaffold
- [ ] Remaining modules: Attendance, Exams & Grades, Payments & Fees,
      Payroll, Analytics Dashboard
- [ ] Student branch transfer with history (`StudentBranchHistory`) —
      deliberately deferred until it's the thing being built, not bare CRUD
- [ ] `TeacherSubject` (which subjects a teacher teaches) — still deferred;
      no module has needed it yet

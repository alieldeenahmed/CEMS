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

## Progress

- [x] Backend solution scaffold (4 Clean Architecture projects)
- [x] Core NuGet packages wired up
- [x] `Branch` / `Room` domain entities
- [x] `ApplicationUser` (Identity) + `UserBranchAssignment`
- [x] `ApplicationDbContext`
- [x] DI wiring (Program.cs, connection string) + first migration
- [x] Role seeding (Owner, BranchManager, Teacher, FrontDesk, Parent)
- [x] Auth endpoints (register/login, JWT issuing)
- [ ] Branch/Room CRUD endpoints
- [ ] Frontend scaffold
- [ ] Remaining modules: Students, Teachers, Courses & Curriculum, Scheduling &
      Room Booking, Attendance, Exams & Grades, Payments & Fees, Payroll,
      Analytics Dashboard

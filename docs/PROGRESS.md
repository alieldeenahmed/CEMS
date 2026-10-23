# Module status

What exists today, checked against the code (counts are `[Http*]` attributes per controller; routes are from `Frontend/src/app/router.tsx`). Every module has a backend, a page in the frontend, and tests.

| Module | Backend (controller, endpoints) | Frontend route | Notes |
|---|---|---|---|
| Auth | `AuthController` (2) | `/login` | Login; one-time `bootstrap-owner` that disables itself once any user exists |
| Staff & users | `UsersController` (8) | `/staff` | Create staff, activate/deactivate, branch-scoped roster, admin-assisted password reset |
| Branches & rooms | `BranchesController` (5), `RoomsController` (5) | `/branches` | |
| Students & guardians | `StudentsController` (17), `GuardiansController` (4) | `/students`, `/students/:id` | Branch transfer with history, report-card PDF |
| Teachers | `TeachersController` (19) | `/teachers` | Branch assignment, availability, declared qualifications, own schedule |
| Courses & curricula | `CoursesController` (10), `CurriculaController` (5) | `/courses`, `/curricula` | Enrollment with derived capacity and waitlist |
| Scheduling | `SessionsController` (7) | `/scheduling` | Conflict detection, manager override with a reason, teacher substitution, cancel with optional makeup link |
| Attendance | `SessionsController` | `/attendance` | Marking window: from session start until 4 hours after it ends |
| Exams & grades | `ExamsController` (7) | `/exams` | |
| Payments | `InvoicesController` (4), `PackagesController` (5) | `/payments` | Derived invoice status; packages priced per course |
| Payroll | `PayrollRunsController` (5), `StaffPayrollRunsController` (4) | `/payroll` | Four teacher pay types, Draft → Approved → Paid, pay-stub PDF |
| Analytics | `AnalyticsController` (7) | `/analytics`, `/` | Revenue, attendance, enrollment funnel, teacher utilization, PDF and Excel export |

The dashboard (`/`) shows a different summary per role (Owner/Branch Manager, Front Desk, Teacher).

## What is not built

- A guardian/parent portal. Guardians are contact records with no login, by design.
- Email of any kind (no self-service password reset, no invoice reminders).
- Per-branch timezones, and sessions that cross midnight (see the README's limitations).
- Refresh tokens and a "sign out everywhere" feature.
- Deployment: there is no hosted instance and no production deployment configuration. The Docker Compose file is for local demos.

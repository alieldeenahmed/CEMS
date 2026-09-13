import { createBrowserRouter } from 'react-router-dom'
import { AnalyticsPage } from '@/features/analytics/AnalyticsPage'
import { AttendancePage } from '@/features/attendance/AttendancePage'
import { LoginPage } from '@/features/auth/LoginPage'
import { BranchesPage } from '@/features/branches/BranchesPage'
import { HomePage } from '@/features/dashboard/HomePage'
import { CoursesPage } from '@/features/courses/CoursesPage'
import { CurriculaPage } from '@/features/curricula/CurriculaPage'
import { ExamsPage } from '@/features/exams/ExamsPage'
import { PaymentsPage } from '@/features/payments/PaymentsPage'
import { PayrollPage } from '@/features/payroll/PayrollPage'
import { SchedulingPage } from '@/features/scheduling/SchedulingPage'
import { StaffPage } from '@/features/staff/StaffPage'
import { StudentsPage } from '@/features/students/StudentsPage'
import { TeachersPage } from '@/features/teachers/TeachersPage'
import { AppShell } from './layout/AppShell'
import { ProtectedRoute } from './ProtectedRoute'

export const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <AppShell />,
        children: [
          { path: '/', element: <HomePage /> },
          { path: '/branches', element: <BranchesPage /> },
          { path: '/staff', element: <StaffPage /> },
          { path: '/students', element: <StudentsPage /> },
          { path: '/teachers', element: <TeachersPage /> },
          { path: '/courses', element: <CoursesPage /> },
          { path: '/curricula', element: <CurriculaPage /> },
          { path: '/scheduling', element: <SchedulingPage /> },
          { path: '/attendance', element: <AttendancePage /> },
          { path: '/exams', element: <ExamsPage /> },
          { path: '/payments', element: <PaymentsPage /> },
          { path: '/payroll', element: <PayrollPage /> },
          { path: '/analytics', element: <AnalyticsPage /> },
        ],
      },
    ],
  },
])

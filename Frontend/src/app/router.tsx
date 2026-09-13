import { createBrowserRouter } from 'react-router-dom'
import { LoginPage } from '@/features/auth/LoginPage'
import { BranchesPage } from '@/features/branches/BranchesPage'
import { HomePage } from '@/features/dashboard/HomePage'
import { ComingSoonPage } from '@/shared/ComingSoonPage'
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
          { path: '/staff', element: <ComingSoonPage title="Staff" /> },
          { path: '/students', element: <ComingSoonPage title="Students" /> },
          { path: '/teachers', element: <ComingSoonPage title="Teachers" /> },
          { path: '/my-children', element: <ComingSoonPage title="My Children" /> },
          { path: '/courses', element: <ComingSoonPage title="Courses" /> },
          { path: '/curricula', element: <ComingSoonPage title="Curricula" /> },
          { path: '/scheduling', element: <ComingSoonPage title="Scheduling" /> },
          { path: '/attendance', element: <ComingSoonPage title="Attendance" /> },
          { path: '/exams', element: <ComingSoonPage title="Exams & Grades" /> },
          { path: '/payments', element: <ComingSoonPage title="Payments" /> },
          { path: '/payroll', element: <ComingSoonPage title="Payroll" /> },
          { path: '/analytics', element: <ComingSoonPage title="Analytics" /> },
        ],
      },
    ],
  },
])

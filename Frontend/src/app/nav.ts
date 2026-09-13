import {
  BookOpen,
  Building2,
  CalendarClock,
  CircleUserRound,
  ClipboardCheck,
  GraduationCap,
  LayoutDashboard,
  LibraryBig,
  LineChart,
  type LucideIcon,
  Receipt,
  ShieldCheck,
  UsersRound,
  Wallet,
} from 'lucide-react'
import { ROLES, type Role } from '@/features/auth/constants'

export interface NavItem {
  label: string
  path: string
  icon: LucideIcon
  roles: Role[]
}

export interface NavGroup {
  label: string
  items: NavItem[]
}

export const NAV_GROUPS: NavGroup[] = [
  {
    label: 'Overview',
    items: [
      {
        label: 'Dashboard',
        path: '/',
        icon: LayoutDashboard,
        roles: [ROLES.Owner, ROLES.BranchManager, ROLES.Teacher, ROLES.FrontDesk, ROLES.Parent],
      },
    ],
  },
  {
    label: 'Organization',
    items: [
      { label: 'Branches', path: '/branches', icon: Building2, roles: [ROLES.Owner] },
      { label: 'Staff', path: '/staff', icon: ShieldCheck, roles: [ROLES.Owner] },
    ],
  },
  {
    label: 'People',
    items: [
      {
        label: 'Students',
        path: '/students',
        icon: GraduationCap,
        roles: [ROLES.Owner, ROLES.BranchManager, ROLES.FrontDesk],
      },
      {
        label: 'Teachers',
        path: '/teachers',
        icon: UsersRound,
        roles: [ROLES.Owner, ROLES.BranchManager],
      },
      { label: 'My Children', path: '/my-children', icon: CircleUserRound, roles: [ROLES.Parent] },
    ],
  },
  {
    label: 'Academics',
    items: [
      {
        label: 'Courses',
        path: '/courses',
        icon: BookOpen,
        roles: [ROLES.Owner, ROLES.BranchManager, ROLES.FrontDesk],
      },
      {
        label: 'Curricula',
        path: '/curricula',
        icon: LibraryBig,
        roles: [ROLES.Owner, ROLES.BranchManager],
      },
      {
        label: 'Scheduling',
        path: '/scheduling',
        icon: CalendarClock,
        roles: [ROLES.Owner, ROLES.BranchManager, ROLES.FrontDesk, ROLES.Teacher],
      },
      {
        label: 'Attendance',
        path: '/attendance',
        icon: ClipboardCheck,
        roles: [ROLES.Owner, ROLES.BranchManager, ROLES.FrontDesk, ROLES.Teacher],
      },
      {
        label: 'Exams & Grades',
        path: '/exams',
        icon: GraduationCap,
        roles: [ROLES.Owner, ROLES.BranchManager, ROLES.Teacher, ROLES.Parent],
      },
    ],
  },
  {
    label: 'Finance',
    items: [
      {
        label: 'Payments',
        path: '/payments',
        icon: Receipt,
        roles: [ROLES.Owner, ROLES.BranchManager, ROLES.FrontDesk, ROLES.Parent],
      },
      { label: 'Payroll', path: '/payroll', icon: Wallet, roles: [ROLES.Owner] },
      {
        label: 'Analytics',
        path: '/analytics',
        icon: LineChart,
        roles: [ROLES.Owner, ROLES.BranchManager],
      },
    ],
  },
]

import { useAuth } from '@/features/auth/AuthContext'
import { ROLES } from '@/features/auth/constants'
import { FrontDeskOverview } from './FrontDeskOverview'
import { OwnerOverview } from './OwnerOverview'
import { ParentOverview } from './ParentOverview'
import { TeacherOverview } from './TeacherOverview'

export function HomePage() {
  const { user, hasRole } = useAuth()

  return (
    <div>
      <h1 className="font-serif text-2xl font-semibold text-navy">Dashboard</h1>
      <p className="mt-1 text-sm text-muted">
        Welcome back, {user?.fullName}. Here&apos;s what&apos;s happening at CEMS.
      </p>

      <div className="mt-6">
        {hasRole(ROLES.Owner, ROLES.BranchManager) && <OwnerOverview />}
        {hasRole(ROLES.FrontDesk) && <FrontDeskOverview />}
        {hasRole(ROLES.Teacher) && <TeacherOverview />}
        {hasRole(ROLES.Parent) && <ParentOverview />}
      </div>
    </div>
  )
}

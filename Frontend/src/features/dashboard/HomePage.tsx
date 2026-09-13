import { ClipboardCheck, GraduationCap, Receipt, Wallet } from 'lucide-react'
import { useAuth } from '@/features/auth/AuthContext'
import { ROLES } from '@/features/auth/constants'
import { StatCard } from './StatCard'
import { useDashboardSummary } from './useDashboardSummary'

const currencyFormatter = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

function AnalyticsDashboard() {
  const { user } = useAuth()
  const isOwner = user?.roles.includes(ROLES.Owner) ?? false
  const branchId = isOwner ? undefined : user?.branchIds[0]

  const { data, isLoading, isError } = useDashboardSummary(branchId)

  if (isLoading) {
    return <p className="text-sm text-muted">Loading dashboard...</p>
  }

  if (isError || !data) {
    return <p className="text-sm text-coral">Could not load dashboard data.</p>
  }

  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
      <StatCard
        label="Revenue collected"
        value={currencyFormatter.format(data.revenue.totalCollected)}
        icon={Wallet}
      />
      <StatCard
        label="Outstanding balance"
        value={currencyFormatter.format(data.revenue.totalOutstanding)}
        icon={Receipt}
      />
      <StatCard
        label="Attendance rate"
        value={`${(data.attendanceTrends.attendanceRate * 100).toFixed(1)}%`}
        icon={ClipboardCheck}
      />
      <StatCard
        label="Active enrollments"
        value={String(data.enrollmentFunnel.studentsWithActiveEnrollment)}
        icon={GraduationCap}
      />
    </div>
  )
}

export function HomePage() {
  const { user, hasRole } = useAuth()
  const canViewAnalytics = hasRole(ROLES.Owner, ROLES.BranchManager)

  return (
    <div>
      <h1 className="font-serif text-2xl font-semibold text-navy">Dashboard</h1>
      <p className="mt-1 text-sm text-muted">
        Welcome back, {user?.fullName}. Here&apos;s what&apos;s happening at CEMS.
      </p>

      <div className="mt-6">
        {canViewAnalytics ? (
          <AnalyticsDashboard />
        ) : (
          <div className="rounded-lg border border-line bg-paper p-6">
            <p className="text-sm text-muted">
              A personalized overview for your role is coming soon.
            </p>
          </div>
        )}
      </div>
    </div>
  )
}

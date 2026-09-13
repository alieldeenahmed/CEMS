import { useMyStaffPayrollRuns } from './api'
import { StaffPayrollRunsList } from './StaffPayrollRunsList'

export function MyStaffPayrollView() {
  const { data: runs, isLoading, isError } = useMyStaffPayrollRuns()

  if (isLoading) {
    return <p className="text-sm text-muted">Loading your payroll runs...</p>
  }

  if (isError) {
    return <p className="text-sm text-coral">Could not load your payroll runs.</p>
  }

  return <StaffPayrollRunsList runs={runs ?? []} userId={null} canManage={false} />
}

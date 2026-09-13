import { useMyPayrollRuns } from './api'
import { PayrollRunsList } from './PayrollRunsList'

export function TeacherPayrollView() {
  const { data: runs, isLoading, isError } = useMyPayrollRuns()

  if (isLoading) {
    return <p className="text-sm text-muted">Loading your payroll runs...</p>
  }

  if (isError) {
    return <p className="text-sm text-coral">Could not load your payroll runs.</p>
  }

  return <PayrollRunsList runs={runs ?? []} teacherId={null} canManage={false} />
}

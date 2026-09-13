import { useAuth } from '@/features/auth/AuthContext'
import { ROLES } from '@/features/auth/constants'
import { PageHeader } from '@/shared/ui/PageHeader'
import { StaffPayrollView } from './StaffPayrollView'
import { TeacherPayrollView } from './TeacherPayrollView'

export function PayrollPage() {
  const { hasRole } = useAuth()
  const isOwner = hasRole(ROLES.Owner)

  return (
    <div>
      <PageHeader
        title="Payroll"
        description={isOwner ? 'Generate, approve, and pay teacher payroll runs.' : 'Your payroll runs.'}
      />

      {isOwner ? <StaffPayrollView /> : <TeacherPayrollView />}
    </div>
  )
}

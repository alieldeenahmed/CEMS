import { useAuth } from '@/features/auth/AuthContext'
import { ROLES } from '@/features/auth/constants'
import { PageHeader } from '@/shared/ui/PageHeader'
import { MyStaffPayrollView } from './MyStaffPayrollView'
import { OwnerPayrollTabs } from './OwnerPayrollTabs'
import { TeacherPayrollView } from './TeacherPayrollView'

export function PayrollPage() {
  const { hasRole } = useAuth()
  const isOwner = hasRole(ROLES.Owner)
  const isTeacher = hasRole(ROLES.Teacher)

  return (
    <div>
      <PageHeader
        title="Payroll"
        description={isOwner ? 'Generate, approve, and pay payroll runs.' : 'Your payroll runs.'}
      />

      {isOwner ? <OwnerPayrollTabs /> : isTeacher ? <TeacherPayrollView /> : <MyStaffPayrollView />}
    </div>
  )
}

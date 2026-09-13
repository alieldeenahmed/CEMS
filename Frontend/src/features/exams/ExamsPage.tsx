import { useAuth } from '@/features/auth/AuthContext'
import { ROLES } from '@/features/auth/constants'
import { PageHeader } from '@/shared/ui/PageHeader'
import { ParentGradesView } from './ParentGradesView'
import { StaffExamsView } from './StaffExamsView'

export function ExamsPage() {
  const { hasRole } = useAuth()
  const isParent = hasRole(ROLES.Parent) && !hasRole(ROLES.Owner, ROLES.BranchManager, ROLES.FrontDesk, ROLES.Teacher)

  return (
    <div>
      <PageHeader
        title="Exams & Grades"
        description={
          isParent ? "Your children's grades and report cards." : 'Exams, grading, and report cards.'
        }
      />

      {isParent ? <ParentGradesView /> : <StaffExamsView />}
    </div>
  )
}

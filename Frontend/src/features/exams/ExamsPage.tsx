import { PageHeader } from '@/shared/ui/PageHeader'
import { StaffExamsView } from './StaffExamsView'

export function ExamsPage() {
  return (
    <div>
      <PageHeader title="Exams & Grades" description="Exams, grading, and report cards." />
      <StaffExamsView />
    </div>
  )
}

import { BookOpen, Building2, GraduationCap, UsersRound } from 'lucide-react'
import { useAuth } from '@/features/auth/AuthContext'
import { useBranches } from '@/features/branches/api'
import { useCourses } from '@/features/courses/api'
import { useStudents } from '@/features/students/api'
import { useTeachers } from '@/features/teachers/api'
import { StatCard } from '@/shared/ui/StatCard'

export function FrontDeskOverview() {
  const { user } = useAuth()
  const { data: branches } = useBranches()
  const { data: students, isLoading: isLoadingStudents } = useStudents()
  const { data: courses, isLoading: isLoadingCourses } = useCourses()
  const { data: teachers, isLoading: isLoadingTeachers } = useTeachers()

  const branchId = user?.branchIds[0]
  const branchName = branches?.find((b) => b.id === branchId)?.name ?? 'your branch'

  const isLoading = isLoadingStudents || isLoadingCourses || isLoadingTeachers
  const activeStudents = students?.filter((s) => s.status === 'Active').length ?? 0
  const branchTeachers = teachers?.filter((t) => branchId && t.branchIds.includes(branchId)).length ?? 0

  if (isLoading) {
    return <p className="text-sm text-muted">Loading overview...</p>
  }

  return (
    <div>
      <p className="mb-4 flex items-center gap-1.5 text-sm text-muted">
        <Building2 size={15} />
        {branchName}
      </p>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <StatCard label="Active students" value={String(activeStudents)} icon={GraduationCap} />
        <StatCard label="Courses offered" value={String(courses?.length ?? 0)} icon={BookOpen} />
        <StatCard label="Teachers at your branch" value={String(branchTeachers)} icon={UsersRound} />
      </div>
    </div>
  )
}

import { ChevronDown, ChevronRight, Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useBranches } from '@/features/branches/api'
import { useAuth } from '@/features/auth/AuthContext'
import { ROLES } from '@/features/auth/constants'
import { useSubjects } from '@/features/curricula/api'
import { Button } from '@/shared/ui/Button'
import { PageHeader } from '@/shared/ui/PageHeader'
import { getErrorMessage } from '@/shared/api/errors'
import { useCourses, useDeleteCourse } from './api'
import { CourseFormModal } from './CourseFormModal'
import { EnrollmentsSection } from './EnrollmentsSection'
import type { Course } from './types'

export function CoursesPage() {
  const { hasRole } = useAuth()
  const canManageCourses = hasRole(ROLES.Owner, ROLES.BranchManager)
  const { data: courses, isLoading, isError } = useCourses()
  const { data: branches } = useBranches()
  const { data: subjects } = useSubjects()
  const deleteCourse = useDeleteCourse()
  const [expandedId, setExpandedId] = useState<string | null>(null)
  const [modalState, setModalState] = useState<'closed' | 'create' | Course>('closed')

  const branchNameById = new Map(branches?.map((b) => [b.id, b.name]))
  const subjectNameById = new Map(subjects?.map((s) => [s.id, s.name]))

  function handleDelete(course: Course) {
    if (window.confirm(`Delete "${course.name}"? This cannot be undone.`)) {
      deleteCourse.mutate(course.id, {
        onError: (error) => window.alert(getErrorMessage(error, 'Could not delete this course.')),
      })
    }
  }

  return (
    <div>
      <PageHeader
        title="Courses"
        description="Courses offered per branch, and their student enrollments."
        action={
          canManageCourses ? (
            <Button onClick={() => setModalState('create')}>
              <Plus size={15} />
              New course
            </Button>
          ) : undefined
        }
      />

      {isLoading && <p className="text-sm text-muted">Loading courses...</p>}
      {isError && <p className="text-sm text-coral">Could not load courses.</p>}

      {courses && (
        <div className="overflow-hidden rounded-lg border border-line bg-paper">
          {courses.map((course, index) => {
            const isExpanded = expandedId === course.id
            return (
              <div key={course.id} className={index > 0 ? 'border-t border-line' : ''}>
                <div className="flex items-center gap-3 px-4 py-3">
                  <button
                    type="button"
                    onClick={() => setExpandedId(isExpanded ? null : course.id)}
                    aria-label={isExpanded ? 'Collapse' : 'Expand'}
                    className="text-muted transition-colors hover:text-ink"
                  >
                    {isExpanded ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
                  </button>

                  <div className="flex-1">
                    <p className="text-sm font-medium text-ink">{course.name}</p>
                    <p className="text-xs text-muted">
                      {subjectNameById.get(course.subjectId) ?? 'Unknown subject'} ·{' '}
                      {branchNameById.get(course.branchId) ?? 'Unknown branch'}
                    </p>
                  </div>

                  <span className="rounded-full bg-navy/10 px-2.5 py-0.5 text-xs font-medium text-navy">
                    {course.deliveryMode === 'OneOnOne' ? 'One-on-one' : 'Group'}
                  </span>

                  {canManageCourses && (
                    <>
                      <button
                        type="button"
                        aria-label={`Edit ${course.name}`}
                        onClick={() => setModalState(course)}
                        className="text-muted transition-colors hover:text-navy"
                      >
                        <Pencil size={15} />
                      </button>
                      <button
                        type="button"
                        aria-label={`Delete ${course.name}`}
                        onClick={() => handleDelete(course)}
                        className="text-muted transition-colors hover:text-coral"
                      >
                        <Trash2 size={15} />
                      </button>
                    </>
                  )}
                </div>

                {isExpanded && <EnrollmentsSection course={course} />}
              </div>
            )
          })}

          {courses.length === 0 && (
            <p className="px-4 py-6 text-center text-sm text-muted">No courses yet.</p>
          )}
        </div>
      )}

      {modalState !== 'closed' && (
        <CourseFormModal
          course={modalState === 'create' ? null : modalState}
          onClose={() => setModalState('closed')}
        />
      )}
    </div>
  )
}

import { ChevronDown, ChevronRight, Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useAuth } from '@/features/auth/AuthContext'
import { ROLES } from '@/features/auth/constants'
import { useBranches } from '@/features/branches/api'
import { useCourses, useMyCourses } from '@/features/courses/api'
import { getErrorMessage } from '@/shared/api/errors'
import { Button } from '@/shared/ui/Button'
import { Select } from '@/shared/ui/Input'
import { useDeleteExam, useExamsForCourse } from './api'
import { ExamFormModal } from './ExamFormModal'
import { GradesSection } from './GradesSection'
import type { Exam } from './types'

export function StaffExamsView() {
  const { hasRole } = useAuth()
  const isStaff = hasRole(ROLES.Owner, ROLES.BranchManager, ROLES.FrontDesk)
  const canManage = hasRole(ROLES.Owner, ROLES.BranchManager, ROLES.Teacher)

  const { data: staffCourses } = useCourses()
  const { data: myCourses } = useMyCourses()
  const courses = isStaff ? staffCourses : myCourses

  const { data: branches } = useBranches()
  const [courseId, setCourseId] = useState('')
  const { data: exams, isLoading } = useExamsForCourse(courseId || null)
  const deleteExam = useDeleteExam(courseId)
  const [expandedId, setExpandedId] = useState<string | null>(null)
  const [modalState, setModalState] = useState<'closed' | 'create' | Exam>('closed')

  const branchNameById = new Map(branches?.map((b) => [b.id, b.name]))

  function handleDelete(exam: Exam) {
    if (window.confirm(`Delete "${exam.name}"? This cannot be undone.`)) {
      deleteExam.mutate(exam.id, {
        onError: (error) => window.alert(getErrorMessage(error, 'Could not delete this exam.')),
      })
    }
  }

  return (
    <div>
      <div className="mb-4 flex items-center gap-3">
        <div className="w-72">
          <Select value={courseId} onChange={(e) => setCourseId(e.target.value)}>
            <option value="">Select a course...</option>
            {courses?.map((course) => (
              <option key={course.id} value={course.id}>
                {course.name}
                {isStaff && ` · ${branchNameById.get(course.branchId) ?? 'Unknown branch'}`}
              </option>
            ))}
          </Select>
        </div>
        {courseId && canManage && (
          <Button onClick={() => setModalState('create')}>
            <Plus size={15} />
            New exam
          </Button>
        )}
      </div>

      {!courseId && <p className="text-sm text-muted">Choose a course to see its exams.</p>}
      {courseId && isLoading && <p className="text-sm text-muted">Loading exams...</p>}

      {courseId && exams && (
        <div className="overflow-hidden rounded-lg border border-line bg-paper">
          {exams.map((exam, index) => {
            const isExpanded = expandedId === exam.id
            return (
              <div key={exam.id} className={index > 0 ? 'border-t border-line' : ''}>
                <div className="flex items-center gap-3 px-4 py-3">
                  <button
                    type="button"
                    onClick={() => setExpandedId(isExpanded ? null : exam.id)}
                    aria-label={isExpanded ? 'Collapse' : 'Expand'}
                    className="text-muted transition-colors hover:text-ink"
                  >
                    {isExpanded ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
                  </button>

                  <div className="flex-1">
                    <p className="text-sm font-medium text-ink">{exam.name}</p>
                    <p className="text-xs text-muted">
                      {exam.examDate} · out of {exam.maxScore}
                    </p>
                  </div>

                  {canManage && (
                    <>
                      <button
                        type="button"
                        aria-label={`Edit ${exam.name}`}
                        onClick={() => setModalState(exam)}
                        className="text-muted transition-colors hover:text-navy"
                      >
                        <Pencil size={15} />
                      </button>
                      <button
                        type="button"
                        aria-label={`Delete ${exam.name}`}
                        onClick={() => handleDelete(exam)}
                        className="text-muted transition-colors hover:text-coral"
                      >
                        <Trash2 size={15} />
                      </button>
                    </>
                  )}
                </div>

                {isExpanded && <GradesSection exam={exam} canGrade={canManage} />}
              </div>
            )
          })}

          {exams.length === 0 && (
            <p className="px-4 py-6 text-center text-sm text-muted">No exams yet.</p>
          )}
        </div>
      )}

      {modalState !== 'closed' && courseId && (
        <ExamFormModal
          courseId={courseId}
          exam={modalState === 'create' ? null : modalState}
          onClose={() => setModalState('closed')}
        />
      )}
    </div>
  )
}

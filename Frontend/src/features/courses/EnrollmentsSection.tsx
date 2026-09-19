import { ArrowUpCircle, X } from 'lucide-react'
import { useState } from 'react'
import { useStudents } from '@/features/students/api'
import { getErrorMessage } from '@/shared/api/errors'
import { Button } from '@/shared/ui/Button'
import {
  useDropEnrollment,
  useEnrollmentsForCourse,
  useEnrollStudent,
  usePromoteFromWaitlist,
} from './api'
import type { Course } from './types'

const STATUS_CLASSES: Record<string, string> = {
  Active: 'bg-teal/10 text-teal',
  Waitlisted: 'bg-ochre/10 text-ochre',
  Dropped: 'bg-muted/10 text-muted',
}

export function EnrollmentsSection({ course }: { course: Course }) {
  const { data: enrollments, isLoading } = useEnrollmentsForCourse(course.id)
  const { data: students } = useStudents()
  const enrollStudent = useEnrollStudent(course.id)
  const dropEnrollment = useDropEnrollment(course.id)
  const promoteFromWaitlist = usePromoteFromWaitlist(course.id)
  const [selectedStudentId, setSelectedStudentId] = useState('')

  const studentById = new Map(students?.map((s) => [s.id, s]))
  const enrolledStudentIds = new Set(
    enrollments?.filter((e) => e.status !== 'Dropped').map((e) => e.studentId),
  )
  const eligibleStudents =
    students?.filter((s) => s.currentBranchId === course.branchId && !enrolledStudentIds.has(s.id)) ?? []

  const activeEnrollments = enrollments?.filter((e) => e.status !== 'Dropped') ?? []

  if (isLoading) {
    return <p className="text-sm text-muted">Loading enrollments...</p>
  }

  return (
    <div className="border-t border-line bg-parchment/50 p-4">
      <p className="mb-3 text-xs font-medium uppercase tracking-wide text-muted">Enrollments</p>

      {activeEnrollments.length > 0 ? (
        <ul className="mb-4 space-y-1.5">
          {activeEnrollments.map((enrollment) => (
            <li
              key={enrollment.id}
              className="flex items-center justify-between rounded-md bg-paper px-3 py-2 text-sm"
            >
              <span className="text-ink">
                {studentById.get(enrollment.studentId)?.fullName ?? 'Unknown student'}
                {enrollment.status === 'Waitlisted' && enrollment.position != null && (
                  <span className="text-muted"> · position {enrollment.position}</span>
                )}
              </span>

              <div className="flex items-center gap-2">
                <span
                  className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_CLASSES[enrollment.status]}`}
                >
                  {enrollment.status}
                </span>
                {enrollment.status === 'Waitlisted' && (
                  <button
                    type="button"
                    aria-label="Promote from waitlist"
                    onClick={() => promoteFromWaitlist.mutate(enrollment.id)}
                    className="text-muted transition-colors hover:text-teal"
                  >
                    <ArrowUpCircle size={15} />
                  </button>
                )}
                <button
                  type="button"
                  aria-label="Drop enrollment"
                  onClick={() => dropEnrollment.mutate(enrollment.id)}
                  className="text-muted transition-colors hover:text-coral"
                >
                  <X size={15} />
                </button>
              </div>
            </li>
          ))}
        </ul>
      ) : (
        <p className="mb-4 text-sm text-muted">No students enrolled yet.</p>
      )}

      <div className="flex items-center gap-2">
        <select
          value={selectedStudentId}
          onChange={(e) => setSelectedStudentId(e.target.value)}
          className="flex-1 rounded-md border border-line bg-paper px-2 py-1.5 text-sm outline-none focus:border-navy"
        >
          <option value="">Select a student from this branch...</option>
          {eligibleStudents.map((student) => (
            <option key={student.id} value={student.id}>
              {student.fullName}
            </option>
          ))}
        </select>
        <Button
          variant="secondary"
          disabled={!selectedStudentId || enrollStudent.isPending}
          onClick={() => {
            enrollStudent.mutate(selectedStudentId, {
              onError: (error) => window.alert(getErrorMessage(error, 'Could not enroll this student.')),
            })
            setSelectedStudentId('')
          }}
        >
          Enroll
        </Button>
      </div>
    </div>
  )
}

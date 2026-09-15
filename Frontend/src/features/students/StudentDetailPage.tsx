import { ArrowLeft, ArrowRightLeft } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useAuth } from '@/features/auth/AuthContext'
import { ROLES } from '@/features/auth/constants'
import { useBranches } from '@/features/branches/api'
import { useAttendanceForStudent } from '@/features/attendance/api'
import { useEnrollmentsForStudent } from '@/features/courses/api'
import { useGradesForStudent } from '@/features/exams/api'
import { formatUtcForDisplay } from '@/features/scheduling/time'
import { Button } from '@/shared/ui/Button'
import { PageHeader } from '@/shared/ui/PageHeader'
import { useBranchHistoryForStudent, useStudent } from './api'
import { GuardiansSection } from './GuardiansSection'
import { TransferBranchModal } from './TransferBranchModal'

const STUDENT_STATUS_CLASSES: Record<string, string> = {
  Active: 'bg-teal/10 text-teal',
  Paused: 'bg-ochre/10 text-ochre',
  Graduated: 'bg-muted/10 text-muted',
}

const ENROLLMENT_STATUS_CLASSES: Record<string, string> = {
  Active: 'bg-teal/10 text-teal',
  Dropped: 'bg-muted/10 text-muted',
  Waitlisted: 'bg-ochre/10 text-ochre',
}

const ATTENDANCE_STATUS_CLASSES: Record<string, string> = {
  Unmarked: 'bg-muted/10 text-muted',
  Present: 'bg-teal/10 text-teal',
  Absent: 'bg-coral/10 text-coral',
  Late: 'bg-ochre/10 text-ochre',
  Excused: 'bg-navy/10 text-navy',
}

function SectionCard({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="mb-6 overflow-hidden rounded-lg border border-line bg-paper">
      <p className="border-b border-line px-4 py-3 text-xs font-medium uppercase tracking-wide text-muted">
        {title}
      </p>
      {children}
    </div>
  )
}

export function StudentDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { hasRole } = useAuth()
  const canViewFull = hasRole(ROLES.Owner, ROLES.BranchManager, ROLES.FrontDesk)

  const [isTransferOpen, setIsTransferOpen] = useState(false)

  const { data: student, isLoading, isError } = useStudent(id ?? null)
  const { data: branches } = useBranches({ enabled: canViewFull })
  const { data: enrollments, isLoading: enrollmentsLoading } = useEnrollmentsForStudent(id ?? null)
  const { data: attendance, isLoading: attendanceLoading } = useAttendanceForStudent(id ?? null)
  const { data: grades, isLoading: gradesLoading } = useGradesForStudent(id ?? null)
  const { data: branchHistory, isLoading: branchHistoryLoading } = useBranchHistoryForStudent(
    canViewFull ? id ?? null : null,
  )

  const branchNameById = new Map(branches?.map((b) => [b.id, b.name]))

  if (isLoading) {
    return <p className="text-sm text-muted">Loading student...</p>
  }

  if (isError || !student) {
    return <p className="text-sm text-coral">Could not load this student.</p>
  }

  return (
    <div>
      <button
        type="button"
        onClick={() => navigate(-1)}
        className="mb-4 inline-flex items-center gap-1.5 text-sm text-muted transition-colors hover:text-navy"
      >
        <ArrowLeft size={15} />
        Back
      </button>

      <PageHeader
        title={student.fullName}
        description={
          canViewFull
            ? `${student.gender} · born ${student.dateOfBirth} · ${branchNameById.get(student.currentBranchId) ?? 'Unknown branch'}`
            : `${student.gender} · born ${student.dateOfBirth}`
        }
        action={
          <div className="flex items-center gap-3">
            <span
              className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${STUDENT_STATUS_CLASSES[student.status]}`}
            >
              {student.status}
            </span>
            {canViewFull && (
              <Button variant="secondary" onClick={() => setIsTransferOpen(true)}>
                <ArrowRightLeft size={15} />
                Transfer branch
              </Button>
            )}
          </div>
        }
      />

      {canViewFull && (
        <SectionCard title="Guardians">
          <GuardiansSection studentId={student.id} />
        </SectionCard>
      )}

      {canViewFull && (
        <SectionCard title="Branch history">
          {branchHistoryLoading && <p className="px-4 py-4 text-sm text-muted">Loading branch history...</p>}
          {branchHistory && branchHistory.length === 0 && (
            <p className="px-4 py-4 text-sm text-muted">Never transferred branches.</p>
          )}
          {branchHistory && branchHistory.length > 0 && (
            <div>
              {branchHistory.map((entry, index) => (
                <div
                  key={entry.id}
                  className={`flex items-center gap-3 px-4 py-3 ${index > 0 ? 'border-t border-line' : ''}`}
                >
                  <div className="flex-1">
                    <p className="text-sm font-medium text-ink">
                      {entry.fromBranchName} → {entry.toBranchName}
                    </p>
                    <p className="text-xs text-muted">
                      {entry.transferDate}
                      {entry.reason ? ` · ${entry.reason}` : ''}
                    </p>
                  </div>
                </div>
              ))}
            </div>
          )}
        </SectionCard>
      )}

      <SectionCard title="Enrollments">
        {enrollmentsLoading && <p className="px-4 py-4 text-sm text-muted">Loading enrollments...</p>}
        {enrollments && enrollments.length === 0 && (
          <p className="px-4 py-4 text-sm text-muted">No enrollments to show.</p>
        )}
        {enrollments && enrollments.length > 0 && (
          <div>
            {enrollments.map((enrollment, index) => (
              <div
                key={enrollment.id}
                className={`flex items-center gap-3 px-4 py-3 ${index > 0 ? 'border-t border-line' : ''}`}
              >
                <div className="flex-1">
                  <p className="text-sm font-medium text-ink">{enrollment.courseName}</p>
                  <p className="text-xs text-muted">enrolled {enrollment.enrollmentDate}</p>
                </div>
                <span
                  className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${ENROLLMENT_STATUS_CLASSES[enrollment.status]}`}
                >
                  {enrollment.status}
                </span>
              </div>
            ))}
          </div>
        )}
      </SectionCard>

      <SectionCard title="Attendance">
        {attendanceLoading && <p className="px-4 py-4 text-sm text-muted">Loading attendance...</p>}
        {attendance && attendance.length === 0 && (
          <p className="px-4 py-4 text-sm text-muted">No attendance records to show.</p>
        )}
        {attendance && attendance.length > 0 && (
          <div>
            {attendance.map((record, index) => (
              <div
                key={`${record.courseSessionId}-${record.studentId}`}
                className={`flex items-center gap-3 px-4 py-3 ${index > 0 ? 'border-t border-line' : ''}`}
              >
                <div className="flex-1">
                  <p className="text-sm font-medium text-ink">{record.courseName}</p>
                  <p className="text-xs text-muted">{formatUtcForDisplay(record.sessionStartUtc)}</p>
                </div>
                <span
                  className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${ATTENDANCE_STATUS_CLASSES[record.status]}`}
                >
                  {record.status}
                </span>
              </div>
            ))}
          </div>
        )}
      </SectionCard>

      <SectionCard title="Exams & Grades">
        {gradesLoading && <p className="px-4 py-4 text-sm text-muted">Loading grades...</p>}
        {grades && grades.length === 0 && <p className="px-4 py-4 text-sm text-muted">No grades to show.</p>}
        {grades && grades.length > 0 && (
          <div>
            {grades.map((grade, index) => (
              <div
                key={`${grade.examId}-${grade.studentId}`}
                className={`flex items-center gap-3 px-4 py-3 ${index > 0 ? 'border-t border-line' : ''}`}
              >
                <div className="flex-1">
                  <p className="text-sm font-medium text-ink">{grade.examName}</p>
                  <p className="text-xs text-muted">
                    {grade.courseName} · {grade.examDate}
                    {grade.comments ? ` · ${grade.comments}` : ''}
                  </p>
                </div>
                <span className="rounded-full bg-navy/10 px-2.5 py-0.5 text-xs font-medium text-navy">
                  {grade.score === null ? 'Not graded' : `${grade.score} / ${grade.examMaxScore}`}
                </span>
              </div>
            ))}
          </div>
        )}
      </SectionCard>

      {isTransferOpen && <TransferBranchModal student={student} onClose={() => setIsTransferOpen(false)} />}
    </div>
  )
}

import { Plus } from 'lucide-react'
import { useState } from 'react'
import { useBranches } from '@/features/branches/api'
import { useCourses } from '@/features/courses/api'
import { useTeachers } from '@/features/teachers/api'
import { getErrorMessage } from '@/shared/api/errors'
import { Button } from '@/shared/ui/Button'
import { PageHeader } from '@/shared/ui/PageHeader'
import { Select } from '@/shared/ui/Input'
import { useCancelSession, useSessionsForCourse } from './api'
import { CreateSessionModal } from './CreateSessionModal'
import { SubstituteTeacherModal } from './SubstituteTeacherModal'
import { formatUtcForDisplay } from './time'
import type { CourseSession } from './types'

const STATUS_CLASSES: Record<string, string> = {
  Scheduled: 'bg-teal/10 text-teal',
  Completed: 'bg-navy/10 text-navy',
  Cancelled: 'bg-muted/10 text-muted',
}

export function SchedulingPage() {
  const { data: courses } = useCourses()
  const { data: branches } = useBranches()
  const { data: teachers } = useTeachers()
  const [courseId, setCourseId] = useState('')
  const [isCreateOpen, setIsCreateOpen] = useState(false)
  const [cancelingSessionId, setCancelingSessionId] = useState<string | null>(null)
  const [substitutingSession, setSubstitutingSession] = useState<CourseSession | null>(null)

  const { data: sessions, isLoading } = useSessionsForCourse(courseId || null)
  const cancelSession = useCancelSession(courseId)

  const selectedCourse = courses?.find((c) => c.id === courseId) ?? null
  const branchNameById = new Map(branches?.map((b) => [b.id, b.name]))
  const teacherNameById = new Map(teachers?.map((t) => [t.id, t.fullName]))

  const scheduledSessions = sessions?.filter((s) => s.status === 'Scheduled') ?? []

  function handleCancel(session: CourseSession, rescheduledToSessionId: string | null) {
    cancelSession.mutate(
      { id: session.id, rescheduledToSessionId },
      { onError: (error) => window.alert(getErrorMessage(error, 'Could not cancel this session.')) },
    )
    setCancelingSessionId(null)
  }

  return (
    <div>
      <PageHeader
        title="Scheduling"
        description="Book course sessions, checking room, teacher, and availability conflicts."
      />

      <div className="mb-4 flex items-center gap-3">
        <div className="w-72">
          <Select value={courseId} onChange={(e) => setCourseId(e.target.value)}>
            <option value="">Select a course...</option>
            {courses?.map((course) => (
              <option key={course.id} value={course.id}>
                {course.name} · {branchNameById.get(course.branchId) ?? 'Unknown branch'}
              </option>
            ))}
          </Select>
        </div>
        {selectedCourse && (
          <Button onClick={() => setIsCreateOpen(true)}>
            <Plus size={15} />
            New session
          </Button>
        )}
      </div>

      {!selectedCourse && <p className="text-sm text-muted">Choose a course to see its sessions.</p>}

      {selectedCourse && isLoading && <p className="text-sm text-muted">Loading sessions...</p>}

      {selectedCourse && sessions && (
        <div className="overflow-hidden rounded-lg border border-line bg-paper">
          {sessions.map((session, index) => (
            <div
              key={session.id}
              className={`flex items-center gap-3 px-4 py-3 ${index > 0 ? 'border-t border-line' : ''}`}
            >
              <div className="flex-1">
                <p className="text-sm font-medium text-ink">
                  {formatUtcForDisplay(session.startUtc)} – {formatUtcForDisplay(session.endUtc)}
                </p>
                <p className="text-xs text-muted">
                  {teacherNameById.get(session.teacherId) ?? 'Unknown teacher'}
                  {session.overridden && ' · scheduled with an override'}
                </p>
              </div>

              <span
                className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_CLASSES[session.status]}`}
              >
                {session.status}
              </span>

              {session.status === 'Scheduled' &&
                (cancelingSessionId === session.id ? (
                  <CancelSessionRow
                    session={session}
                    alternatives={scheduledSessions.filter((s) => s.id !== session.id)}
                    onConfirm={(rescheduledToSessionId) => handleCancel(session, rescheduledToSessionId)}
                    onDismiss={() => setCancelingSessionId(null)}
                  />
                ) : (
                  <>
                    {new Date(session.startUtc) > new Date() && (
                      <Button variant="secondary" onClick={() => setSubstitutingSession(session)}>
                        Substitute teacher
                      </Button>
                    )}
                    <Button variant="secondary" onClick={() => setCancelingSessionId(session.id)}>
                      Cancel
                    </Button>
                  </>
                ))}
            </div>
          ))}

          {sessions.length === 0 && (
            <p className="px-4 py-6 text-center text-sm text-muted">No sessions scheduled yet.</p>
          )}
        </div>
      )}

      {isCreateOpen && selectedCourse && (
        <CreateSessionModal course={selectedCourse} onClose={() => setIsCreateOpen(false)} />
      )}

      {substitutingSession && selectedCourse && (
        <SubstituteTeacherModal
          session={substitutingSession}
          courseId={selectedCourse.id}
          branchId={selectedCourse.branchId}
          onClose={() => setSubstitutingSession(null)}
        />
      )}
    </div>
  )
}

function CancelSessionRow({
  alternatives,
  onConfirm,
  onDismiss,
}: {
  session: CourseSession
  alternatives: CourseSession[]
  onConfirm: (rescheduledToSessionId: string | null) => void
  onDismiss: () => void
}) {
  const [rescheduledToSessionId, setRescheduledToSessionId] = useState('')

  return (
    <div className="flex items-center gap-2">
      {alternatives.length > 0 && (
        <select
          value={rescheduledToSessionId}
          onChange={(e) => setRescheduledToSessionId(e.target.value)}
          className="rounded-md border border-line bg-paper px-2 py-1.5 text-xs outline-none focus:border-navy"
        >
          <option value="">No makeup session</option>
          {alternatives.map((alt) => (
            <option key={alt.id} value={alt.id}>
              Reschedule to {formatUtcForDisplay(alt.startUtc)}
            </option>
          ))}
        </select>
      )}
      <Button variant="danger" onClick={() => onConfirm(rescheduledToSessionId || null)}>
        Confirm cancel
      </Button>
      <Button variant="ghost" onClick={onDismiss}>
        Dismiss
      </Button>
    </div>
  )
}

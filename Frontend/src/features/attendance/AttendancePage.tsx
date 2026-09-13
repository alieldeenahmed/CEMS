import { useState } from 'react'
import { useAuth } from '@/features/auth/AuthContext'
import { ROLES } from '@/features/auth/constants'
import { useBranches } from '@/features/branches/api'
import { useCourses } from '@/features/courses/api'
import { useSessionsForCourse } from '@/features/scheduling/api'
import { formatUtcForDisplay } from '@/features/scheduling/time'
import { useMySchedule } from '@/features/teachers/api'
import { PageHeader } from '@/shared/ui/PageHeader'
import { Select } from '@/shared/ui/Input'
import { AttendanceGrid } from './AttendanceGrid'

function CourseScopedPicker({ onSelect }: { onSelect: (sessionId: string) => void }) {
  const { data: courses } = useCourses()
  const { data: branches } = useBranches()
  const [courseId, setCourseId] = useState('')
  const { data: sessions } = useSessionsForCourse(courseId || null)

  const branchNameById = new Map(branches?.map((b) => [b.id, b.name]))
  const now = new Date().toISOString()
  const markableSessions = sessions?.filter((s) => s.status !== 'Cancelled' && s.startUtc <= now) ?? []

  return (
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
      {courseId && (
        <div className="w-72">
          <Select onChange={(e) => onSelect(e.target.value)} defaultValue="">
            <option value="">Select a session...</option>
            {markableSessions.map((session) => (
              <option key={session.id} value={session.id}>
                {formatUtcForDisplay(session.startUtc)} ({session.status})
              </option>
            ))}
          </Select>
        </div>
      )}
    </div>
  )
}

function MySchedulePicker({ onSelect }: { onSelect: (sessionId: string) => void }) {
  const { data: sessions } = useMySchedule()
  const now = new Date().toISOString()
  const markableSessions = sessions?.filter((s) => s.status !== 'Cancelled' && s.startUtc <= now) ?? []

  return (
    <div className="mb-4 w-72">
      <Select onChange={(e) => onSelect(e.target.value)} defaultValue="">
        <option value="">Select one of your sessions...</option>
        {markableSessions.map((session) => (
          <option key={session.id} value={session.id}>
            {formatUtcForDisplay(session.startUtc)} ({session.status})
          </option>
        ))}
      </Select>
    </div>
  )
}

export function AttendancePage() {
  const { hasRole } = useAuth()
  const isStaff = hasRole(ROLES.Owner, ROLES.BranchManager, ROLES.FrontDesk)
  const canMark = hasRole(ROLES.Owner, ROLES.BranchManager, ROLES.Teacher)
  const [sessionId, setSessionId] = useState<string | null>(null)

  return (
    <div>
      <PageHeader title="Attendance" description="Mark attendance for a scheduled session." />

      {isStaff ? (
        <CourseScopedPicker onSelect={setSessionId} />
      ) : (
        <MySchedulePicker onSelect={setSessionId} />
      )}

      {sessionId ? (
        <AttendanceGrid sessionId={sessionId} canMark={canMark} />
      ) : (
        <p className="text-sm text-muted">Choose a session to see its roster.</p>
      )}
    </div>
  )
}

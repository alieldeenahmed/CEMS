import { getErrorMessage } from '@/shared/api/errors'
import { useAttendanceForSession, useMarkAttendance } from './api'
import type { AttendanceStatus } from './types'

const STATUS_OPTIONS: AttendanceStatus[] = ['Unmarked', 'Present', 'Absent', 'Late', 'Excused']

const STATUS_CLASSES: Record<AttendanceStatus, string> = {
  Unmarked: 'border-line text-muted',
  Present: 'border-teal text-teal',
  Absent: 'border-coral text-coral',
  Late: 'border-ochre text-ochre',
  Excused: 'border-navy text-navy',
}

export function AttendanceGrid({ sessionId, canMark }: { sessionId: string; canMark: boolean }) {
  const { data: records, isLoading, isError } = useAttendanceForSession(sessionId)
  const markAttendance = useMarkAttendance(sessionId)

  if (isLoading) {
    return <p className="text-sm text-muted">Loading roster...</p>
  }

  if (isError) {
    return <p className="text-sm text-coral">Could not load attendance for this session.</p>
  }

  return (
    <div className="overflow-hidden rounded-lg border border-line bg-paper">
      {records?.map((record, index) => (
        <div
          key={record.studentId}
          className={`flex items-center justify-between px-4 py-3 ${index > 0 ? 'border-t border-line' : ''}`}
        >
          <p className="text-sm font-medium text-ink">{record.studentFullName}</p>

          <select
            value={record.status}
            disabled={!canMark || markAttendance.isPending}
            onChange={(e) =>
              markAttendance.mutate(
                { studentId: record.studentId, status: e.target.value as AttendanceStatus },
                { onError: (error) => window.alert(getErrorMessage(error, 'Could not update attendance.')) },
              )
            }
            className={`rounded-md border bg-paper px-2.5 py-1 text-sm font-medium outline-none disabled:opacity-60 ${STATUS_CLASSES[record.status]}`}
          >
            {STATUS_OPTIONS.map((status) => (
              <option key={status} value={status}>
                {status}
              </option>
            ))}
          </select>
        </div>
      ))}

      {records?.length === 0 && (
        <p className="px-4 py-6 text-center text-sm text-muted">No students enrolled in this course.</p>
      )}
    </div>
  )
}

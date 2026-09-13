export type AttendanceStatus = 'Unmarked' | 'Present' | 'Absent' | 'Late' | 'Excused'

export interface AttendanceRecord {
  id: string | null
  courseSessionId: string
  studentId: string
  studentFullName: string
  status: AttendanceStatus
  markedAtUtc: string | null
  markedByUserId: string | null
}

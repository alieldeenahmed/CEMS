export type SessionStatus = 'Scheduled' | 'Completed' | 'Cancelled'

export interface CourseSession {
  id: string
  courseId: string
  roomId: string
  teacherId: string
  startUtc: string
  endUtc: string
  status: SessionStatus
  overridden: boolean
  overrideReason: string | null
  rescheduledToSessionId: string | null
}

export interface CreateSessionInput {
  roomId: string
  teacherId: string
  startUtc: string
  endUtc: string
  override: boolean
  overrideReason: string | null
}

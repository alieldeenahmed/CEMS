const ATTENDANCE_WINDOW_HOURS_AFTER_END = 4

export function isWithinAttendanceWindow(session: { startUtc: string; endUtc: string }) {
  const now = Date.now()
  const start = new Date(session.startUtc).getTime()
  const closesAt = new Date(session.endUtc).getTime() + ATTENDANCE_WINDOW_HOURS_AFTER_END * 60 * 60 * 1000
  return start <= now && now <= closesAt
}

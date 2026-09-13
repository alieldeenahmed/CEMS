import { BookOpen, CalendarClock, Wallet } from 'lucide-react'
import { formatUtcForDisplay } from '@/features/scheduling/time'
import { useMyCourses } from '@/features/courses/api'
import { useMyPayrollRuns, downloadPayStub } from '@/features/payroll/api'
import { useMySchedule } from '@/features/teachers/api'
import { Button } from '@/shared/ui/Button'
import { StatCard } from '@/shared/ui/StatCard'

export function TeacherOverview() {
  const { data: schedule, isLoading: isLoadingSchedule } = useMySchedule()
  const { data: courses, isLoading: isLoadingCourses } = useMyCourses()
  const { data: payrollRuns, isLoading: isLoadingPayroll } = useMyPayrollRuns()

  const isLoading = isLoadingSchedule || isLoadingCourses || isLoadingPayroll

  if (isLoading) {
    return <p className="text-sm text-muted">Loading overview...</p>
  }

  const now = new Date().toISOString()
  const upcomingSessions =
    schedule
      ?.filter((s) => s.status === 'Scheduled' && s.startUtc >= now)
      .sort((a, b) => a.startUtc.localeCompare(b.startUtc))
      .slice(0, 5) ?? []

  const latestRun = payrollRuns?.[0] ?? null

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <StatCard label="Upcoming sessions" value={String(upcomingSessions.length)} icon={CalendarClock} />
        <StatCard label="Courses taught" value={String(courses?.length ?? 0)} icon={BookOpen} />
        <StatCard
          label="Latest payroll run"
          value={latestRun ? `$${latestRun.totalAmount} · ${latestRun.status}` : 'None yet'}
          icon={Wallet}
        />
      </div>

      <div className="overflow-hidden rounded-lg border border-line bg-paper">
        <p className="border-b border-line p-4 font-serif text-lg font-semibold text-navy">
          Upcoming sessions
        </p>
        {upcomingSessions.length > 0 ? (
          upcomingSessions.map((session, index) => (
            <div
              key={session.id}
              className={`px-4 py-3 text-sm text-ink ${index > 0 ? 'border-t border-line' : ''}`}
            >
              {formatUtcForDisplay(session.startUtc)} – {formatUtcForDisplay(session.endUtc)}
            </div>
          ))
        ) : (
          <p className="px-4 py-6 text-center text-sm text-muted">No upcoming sessions scheduled.</p>
        )}
      </div>

      {latestRun && (
        <Button
          variant="secondary"
          onClick={() =>
            downloadPayStub(latestRun.id, latestRun.periodStart, latestRun.periodEnd).catch(() =>
              window.alert('Could not download this pay stub.'),
            )
          }
        >
          Download latest pay stub
        </Button>
      )}
    </div>
  )
}

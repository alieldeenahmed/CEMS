import { ClipboardCheck, Download, FileSpreadsheet, GraduationCap, Receipt, Wallet } from 'lucide-react'
import { useState } from 'react'
import { useAuth } from '@/features/auth/AuthContext'
import { ROLES } from '@/features/auth/constants'
import { useBranches } from '@/features/branches/api'
import { Button } from '@/shared/ui/Button'
import { Input, Select } from '@/shared/ui/Input'
import { PageHeader } from '@/shared/ui/PageHeader'
import { StatCard } from '@/shared/ui/StatCard'
import { downloadDashboardExcel, downloadDashboardPdf, useAnalyticsDashboard } from './api'

const currencyFormatter = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

function currentMonthRange() {
  const now = new Date()
  const start = new Date(now.getFullYear(), now.getMonth(), 1)
  const end = new Date(now.getFullYear(), now.getMonth() + 1, 0)
  const toDateOnly = (date: Date) => date.toISOString().slice(0, 10)
  return { periodStart: toDateOnly(start), periodEnd: toDateOnly(end) }
}

export function AnalyticsPage() {
  const { user, hasRole } = useAuth()
  const isOwner = hasRole(ROLES.Owner)
  const { data: branches } = useBranches()

  const defaultRange = currentMonthRange()
  const [periodStart, setPeriodStart] = useState(defaultRange.periodStart)
  const [periodEnd, setPeriodEnd] = useState(defaultRange.periodEnd)
  const [branchId, setBranchId] = useState<string>(isOwner ? '' : user?.branchIds[0] ?? '')

  const effectiveBranchId = branchId || undefined
  const { data, isLoading, isError } = useAnalyticsDashboard(effectiveBranchId, periodStart, periodEnd)

  const branchNameById = new Map(branches?.map((b) => [b.id, b.name]))

  return (
    <div>
      <PageHeader title="Analytics" description="Revenue, attendance, enrollment, and teacher utilization." />

      <div className="mb-6 flex flex-wrap items-end gap-3">
        <div className="w-40">
          <Input
            label="From"
            type="date"
            value={periodStart}
            onChange={(e) => setPeriodStart(e.target.value)}
          />
        </div>
        <div className="w-40">
          <Input label="To" type="date" value={periodEnd} onChange={(e) => setPeriodEnd(e.target.value)} />
        </div>

        {isOwner ? (
          <div className="w-56">
            <Select label="Branch" value={branchId} onChange={(e) => setBranchId(e.target.value)}>
              <option value="">All branches</option>
              {branches?.map((branch) => (
                <option key={branch.id} value={branch.id}>
                  {branch.name}
                </option>
              ))}
            </Select>
          </div>
        ) : (
          <p className="text-sm text-muted">
            Branch: <span className="font-medium text-ink">{branchNameById.get(branchId) ?? '—'}</span>
          </p>
        )}

        <div className="ml-auto flex gap-2">
          <Button
            variant="secondary"
            onClick={() => downloadDashboardPdf(effectiveBranchId, periodStart, periodEnd)}
          >
            <Download size={15} />
            PDF
          </Button>
          <Button
            variant="secondary"
            onClick={() => downloadDashboardExcel(effectiveBranchId, periodStart, periodEnd)}
          >
            <FileSpreadsheet size={15} />
            Excel
          </Button>
        </div>
      </div>

      {isLoading && <p className="text-sm text-muted">Loading analytics...</p>}
      {isError && <p className="text-sm text-coral">Could not load analytics for this period.</p>}

      {data && (
        <div className="space-y-6">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <StatCard
              label="Invoiced"
              value={currencyFormatter.format(data.revenue.totalInvoiced)}
              icon={Receipt}
            />
            <StatCard
              label="Collected"
              value={currencyFormatter.format(data.revenue.totalCollected)}
              icon={Wallet}
            />
            <StatCard
              label="Outstanding"
              value={currencyFormatter.format(data.revenue.totalOutstanding)}
              icon={Receipt}
            />
            <StatCard
              label="Attendance rate"
              value={`${(data.attendanceTrends.attendanceRate * 100).toFixed(1)}%`}
              icon={ClipboardCheck}
            />
          </div>

          <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
            <div className="rounded-lg border border-line bg-paper p-5">
              <p className="mb-3 font-serif text-lg font-semibold text-navy">Attendance breakdown</p>
              <dl className="space-y-1.5 text-sm">
                <div className="flex justify-between">
                  <dt className="text-muted">Present</dt>
                  <dd className="text-ink">{data.attendanceTrends.presentCount}</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-muted">Absent</dt>
                  <dd className="text-ink">{data.attendanceTrends.absentCount}</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-muted">Late</dt>
                  <dd className="text-ink">{data.attendanceTrends.lateCount}</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-muted">Excused</dt>
                  <dd className="text-ink">{data.attendanceTrends.excusedCount}</dd>
                </div>
                <div className="flex justify-between border-t border-line pt-1.5 font-medium">
                  <dt className="text-ink">Total records</dt>
                  <dd className="text-ink">{data.attendanceTrends.totalRecords}</dd>
                </div>
              </dl>
            </div>

            <div className="rounded-lg border border-line bg-paper p-5">
              <p className="mb-3 font-serif text-lg font-semibold text-navy">Enrollment funnel</p>
              <dl className="space-y-1.5 text-sm">
                <div className="flex justify-between">
                  <dt className="text-muted">Total students</dt>
                  <dd className="text-ink">{data.enrollmentFunnel.totalStudents}</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-muted">With any enrollment</dt>
                  <dd className="text-ink">{data.enrollmentFunnel.studentsWithAnyEnrollment}</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-muted">With active enrollment</dt>
                  <dd className="text-ink">{data.enrollmentFunnel.studentsWithActiveEnrollment}</dd>
                </div>
              </dl>
            </div>
          </div>

          <div className="overflow-hidden rounded-lg border border-line bg-paper">
            <p className="border-b border-line p-4 font-serif text-lg font-semibold text-navy">
              <GraduationCap className="mr-1.5 inline-block text-ochre" size={18} />
              Teacher utilization
            </p>
            {data.teacherUtilization.map((row, index) => (
              <div
                key={row.teacherId}
                className={`flex items-center justify-between px-4 py-3 text-sm ${index > 0 ? 'border-t border-line' : ''}`}
              >
                <span className="font-medium text-ink">{row.teacherFullName}</span>
                <span className="text-muted">
                  {row.scheduledHours}h / {row.availableHours}h ·{' '}
                  <span className="font-medium text-ink">{(row.utilizationRate * 100).toFixed(1)}%</span>
                </span>
              </div>
            ))}

            {data.teacherUtilization.length === 0 && (
              <p className="px-4 py-6 text-center text-sm text-muted">No teacher activity in this period.</p>
            )}
          </div>
        </div>
      )}
    </div>
  )
}

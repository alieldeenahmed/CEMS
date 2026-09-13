import { Printer } from 'lucide-react'
import { getErrorMessage } from '@/shared/api/errors'
import { Button } from '@/shared/ui/Button'
import { downloadStaffPayStub, useApproveStaffPayrollRun, useMarkStaffPayrollRunPaid } from './api'
import type { StaffPayrollRun } from './types'

const STATUS_CLASSES: Record<string, string> = {
  Draft: 'bg-ochre/10 text-ochre',
  Approved: 'bg-navy/10 text-navy',
  Paid: 'bg-teal/10 text-teal',
}

interface StaffPayrollRunsListProps {
  runs: StaffPayrollRun[]
  userId: string | null
  canManage: boolean
}

export function StaffPayrollRunsList({ runs, userId, canManage }: StaffPayrollRunsListProps) {
  const approveRun = useApproveStaffPayrollRun(userId ?? '')
  const markPaid = useMarkStaffPayrollRunPaid(userId ?? '')

  return (
    <div className="overflow-hidden rounded-lg border border-line bg-paper">
      {runs.map((run, index) => (
        <div
          key={run.id}
          className={`flex items-center gap-3 px-4 py-3 ${index > 0 ? 'border-t border-line' : ''}`}
        >
          <div className="flex-1">
            <p className="text-sm font-medium text-ink">
              {run.periodStart} – {run.periodEnd}
            </p>
            <p className="text-xs text-muted">${run.amount}</p>
          </div>

          <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_CLASSES[run.status]}`}>
            {run.status}
          </span>

          <Button
            variant="secondary"
            onClick={() =>
              downloadStaffPayStub(run.id, run.periodStart, run.periodEnd).catch(() =>
                window.alert('Could not download this pay stub.'),
              )
            }
          >
            <Printer size={15} />
            Pay stub
          </Button>

          {canManage && run.status === 'Draft' && (
            <Button
              variant="secondary"
              onClick={() =>
                approveRun.mutate(run.id, {
                  onError: (error) => window.alert(getErrorMessage(error, 'Could not approve this run.')),
                })
              }
            >
              Approve
            </Button>
          )}

          {canManage && run.status === 'Approved' && (
            <Button
              variant="secondary"
              onClick={() =>
                markPaid.mutate(run.id, {
                  onError: (error) => window.alert(getErrorMessage(error, 'Could not mark this run paid.')),
                })
              }
            >
              Mark paid
            </Button>
          )}
        </div>
      ))}

      {runs.length === 0 && (
        <p className="px-4 py-6 text-center text-sm text-muted">No payroll runs yet.</p>
      )}
    </div>
  )
}

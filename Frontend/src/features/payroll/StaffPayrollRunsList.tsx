import { Pencil, Printer } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { getErrorMessage } from '@/shared/api/errors'
import { Button } from '@/shared/ui/Button'
import { Input } from '@/shared/ui/Input'
import {
  downloadStaffPayStub,
  useApproveStaffPayrollRun,
  useMarkStaffPayrollRunPaid,
  useUpdateStaffPayrollRun,
} from './api'
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
  const updateRun = useUpdateStaffPayrollRun(userId ?? '')
  const [editingRunId, setEditingRunId] = useState<string | null>(null)

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

            {editingRunId === run.id ? (
              <AmountEditRow
                initialAmount={run.amount}
                onCancel={() => setEditingRunId(null)}
                onSave={(amount) =>
                  updateRun.mutate(
                    { runId: run.id, amount },
                    {
                      onSuccess: () => setEditingRunId(null),
                      onError: (error) => window.alert(getErrorMessage(error, 'Could not update this amount.')),
                    },
                  )
                }
              />
            ) : (
              <p className="text-xs text-muted">${run.amount}</p>
            )}
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

          {canManage && run.status === 'Draft' && editingRunId !== run.id && (
            <button
              type="button"
              aria-label="Edit amount"
              onClick={() => setEditingRunId(run.id)}
              className="text-muted transition-colors hover:text-navy"
            >
              <Pencil size={15} />
            </button>
          )}

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

function AmountEditRow({
  initialAmount,
  onSave,
  onCancel,
}: {
  initialAmount: number
  onSave: (amount: number) => void
  onCancel: () => void
}) {
  const [value, setValue] = useState(String(initialAmount))

  function handleSubmit(e: FormEvent) {
    e.preventDefault()
    const amount = Number(value)
    if (amount > 0) {
      onSave(amount)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="mt-1 flex items-center gap-2">
      <Input
        type="number"
        step="0.01"
        min="0.01"
        value={value}
        onChange={(e) => setValue(e.target.value)}
        className="w-28 py-1 text-xs"
        autoFocus
      />
      <Button type="submit" variant="secondary" className="px-2 py-1 text-xs">
        Save
      </Button>
      <Button type="button" variant="ghost" className="px-2 py-1 text-xs" onClick={onCancel}>
        Cancel
      </Button>
    </form>
  )
}

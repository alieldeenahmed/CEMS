import { ChevronDown, ChevronRight } from 'lucide-react'
import { useState } from 'react'
import { getErrorMessage } from '@/shared/api/errors'
import { Button } from '@/shared/ui/Button'
import { useApprovePayrollRun, useMarkPayrollRunPaid } from './api'
import { LineItemsSection } from './LineItemsSection'
import type { PayrollRun } from './types'

const STATUS_CLASSES: Record<string, string> = {
  Draft: 'bg-ochre/10 text-ochre',
  Approved: 'bg-navy/10 text-navy',
  Paid: 'bg-teal/10 text-teal',
}

interface PayrollRunsListProps {
  runs: PayrollRun[]
  teacherId: string | null
  canManage: boolean
}

export function PayrollRunsList({ runs, teacherId, canManage }: PayrollRunsListProps) {
  const [expandedId, setExpandedId] = useState<string | null>(null)
  const approveRun = useApprovePayrollRun(teacherId ?? '')
  const markPaid = useMarkPayrollRunPaid(teacherId ?? '')

  return (
    <div className="overflow-hidden rounded-lg border border-line bg-paper">
      {runs.map((run, index) => {
        const isExpanded = expandedId === run.id
        return (
          <div key={run.id} className={index > 0 ? 'border-t border-line' : ''}>
            <div className="flex items-center gap-3 px-4 py-3">
              <button
                type="button"
                onClick={() => setExpandedId(isExpanded ? null : run.id)}
                aria-label={isExpanded ? 'Collapse' : 'Expand'}
                className="text-muted transition-colors hover:text-ink"
              >
                {isExpanded ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
              </button>

              <div className="flex-1">
                <p className="text-sm font-medium text-ink">
                  {run.periodStart} – {run.periodEnd}
                </p>
                <p className="text-xs text-muted">${run.totalAmount}</p>
              </div>

              <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_CLASSES[run.status]}`}>
                {run.status}
              </span>

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

            {isExpanded && <LineItemsSection runId={run.id} />}
          </div>
        )
      })}

      {runs.length === 0 && (
        <p className="px-4 py-6 text-center text-sm text-muted">No payroll runs yet.</p>
      )}
    </div>
  )
}

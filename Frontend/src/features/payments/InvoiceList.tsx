import { ChevronDown, ChevronRight } from 'lucide-react'
import { useState } from 'react'
import { getErrorMessage } from '@/shared/api/errors'
import { Button } from '@/shared/ui/Button'
import { useCancelInvoice, useInvoicesForStudent } from './api'
import { PaymentsSection } from './PaymentsSection'
import type { Invoice } from './types'

const STATUS_CLASSES: Record<string, string> = {
  Pending: 'bg-ochre/10 text-ochre',
  PartiallyPaid: 'bg-navy/10 text-navy',
  Paid: 'bg-teal/10 text-teal',
  Cancelled: 'bg-muted/10 text-muted',
}

interface InvoiceListProps {
  studentId: string
  canRecordPayment: boolean
  canCancel: boolean
}

export function InvoiceList({ studentId, canRecordPayment, canCancel }: InvoiceListProps) {
  const { data: invoices, isLoading, isError } = useInvoicesForStudent(studentId)
  const cancelInvoice = useCancelInvoice(studentId)
  const [expandedId, setExpandedId] = useState<string | null>(null)

  function handleCancel(invoice: Invoice) {
    if (window.confirm('Cancel this invoice? This cannot be undone.')) {
      cancelInvoice.mutate(invoice.id, {
        onError: (error) => window.alert(getErrorMessage(error, 'Could not cancel this invoice.')),
      })
    }
  }

  if (isLoading) {
    return <p className="text-sm text-muted">Loading invoices...</p>
  }

  if (isError) {
    return <p className="text-sm text-coral">Could not load invoices.</p>
  }

  return (
    <div className="overflow-hidden rounded-lg border border-line bg-paper">
      {invoices?.map((invoice, index) => {
        const isExpanded = expandedId === invoice.id
        return (
          <div key={invoice.id} className={index > 0 ? 'border-t border-line' : ''}>
            <div className="flex items-center gap-3 px-4 py-3">
              {(canRecordPayment || canCancel) && (
                <button
                  type="button"
                  onClick={() => setExpandedId(isExpanded ? null : invoice.id)}
                  aria-label={isExpanded ? 'Collapse' : 'Expand'}
                  className="text-muted transition-colors hover:text-ink"
                >
                  {isExpanded ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
                </button>
              )}

              <div className="flex-1">
                <p className="text-sm font-medium text-ink">
                  ${invoice.amount} · due {invoice.dueDate}
                </p>
                <p className="text-xs text-muted">
                  Paid ${invoice.amountPaid} · balance ${invoice.balanceRemaining}
                  {invoice.isOverdue && ' · overdue'}
                </p>
              </div>

              <span
                className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_CLASSES[invoice.status]}`}
              >
                {invoice.status}
              </span>

              {canCancel && invoice.status !== 'Paid' && invoice.status !== 'Cancelled' && (
                <Button variant="secondary" onClick={() => handleCancel(invoice)}>
                  Cancel
                </Button>
              )}
            </div>

            {isExpanded && (canRecordPayment || canCancel) && (
              <PaymentsSection
                invoiceId={invoice.id}
                studentId={studentId}
                canRecordPayment={canRecordPayment && invoice.status !== 'Cancelled'}
              />
            )}
          </div>
        )
      })}

      {invoices?.length === 0 && (
        <p className="px-4 py-6 text-center text-sm text-muted">No invoices yet.</p>
      )}
    </div>
  )
}

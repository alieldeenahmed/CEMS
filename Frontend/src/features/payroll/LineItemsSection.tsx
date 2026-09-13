import { useSession } from '@/features/scheduling/api'
import { formatUtcForDisplay } from '@/features/scheduling/time'
import { useLineItemsForPayrollRun } from './api'
import type { PayrollLineItem } from './types'

function LineItemRow({ item }: { item: PayrollLineItem }) {
  const { data: session } = useSession(item.courseSessionId)

  return (
    <li className="flex items-center justify-between rounded-md bg-paper px-3 py-2 text-sm">
      <span className="text-ink">
        {session ? formatUtcForDisplay(session.startUtc) : 'Loading session...'}
      </span>
      <span className="font-medium text-ink">${item.amount}</span>
    </li>
  )
}

export function LineItemsSection({ runId }: { runId: string }) {
  const { data: lineItems, isLoading } = useLineItemsForPayrollRun(runId)

  if (isLoading) {
    return <p className="text-sm text-muted">Loading sessions...</p>
  }

  return (
    <div className="border-t border-line bg-parchment/50 p-4">
      <p className="mb-3 text-xs font-medium uppercase tracking-wide text-muted">
        Paid sessions ({lineItems?.length ?? 0})
      </p>

      {lineItems && lineItems.length > 0 ? (
        <ul className="space-y-1.5">
          {lineItems.map((item) => (
            <LineItemRow key={item.id} item={item} />
          ))}
        </ul>
      ) : (
        <p className="text-sm text-muted">No sessions in this run.</p>
      )}
    </div>
  )
}

import { GraduationCap, Receipt } from 'lucide-react'
import { useOutstandingBalance } from '@/features/payments/api'
import { useMyChildren } from '@/features/students/api'
import { StatCard } from '@/shared/ui/StatCard'
import type { Student } from '@/features/students/types'

const currencyFormatter = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

function ChildBalanceRow({ child, isLast }: { child: Student; isLast: boolean }) {
  const { data: balance, isLoading } = useOutstandingBalance(child.id)

  return (
    <div className={`flex items-center justify-between px-4 py-3 text-sm ${isLast ? '' : 'border-b border-line'}`}>
      <span className="font-medium text-ink">{child.fullName}</span>
      <span className="text-muted">
        {isLoading ? 'Loading...' : currencyFormatter.format(balance?.totalOutstanding ?? 0)}
      </span>
    </div>
  )
}

export function ParentOverview() {
  const { data: children, isLoading } = useMyChildren()

  if (isLoading) {
    return <p className="text-sm text-muted">Loading overview...</p>
  }

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <StatCard label="Children" value={String(children?.length ?? 0)} icon={GraduationCap} />
        <StatCard label="Active enrollments" value={String(children?.filter((c) => c.status === 'Active').length ?? 0)} icon={Receipt} />
      </div>

      <div className="overflow-hidden rounded-lg border border-line bg-paper">
        <p className="border-b border-line p-4 font-serif text-lg font-semibold text-navy">
          Outstanding balance by child
        </p>
        {children && children.length > 0 ? (
          children.map((child, index) => (
            <ChildBalanceRow key={child.id} child={child} isLast={index === children.length - 1} />
          ))
        ) : (
          <p className="px-4 py-6 text-center text-sm text-muted">No children linked to your account.</p>
        )}
      </div>
    </div>
  )
}

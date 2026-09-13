import { ChevronDown, ChevronRight } from 'lucide-react'
import { useState } from 'react'
import { useOutstandingBalance } from '@/features/payments/api'
import { PageHeader } from '@/shared/ui/PageHeader'
import { useGuardiansForStudent, useMyChildren } from './api'
import type { Student } from './types'

const STATUS_CLASSES: Record<string, string> = {
  Active: 'bg-teal/10 text-teal',
  Paused: 'bg-ochre/10 text-ochre',
  Graduated: 'bg-muted/10 text-muted',
}

const currencyFormatter = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

function ChildDetails({ studentId }: { studentId: string }) {
  const { data: guardians, isLoading: isLoadingGuardians } = useGuardiansForStudent(studentId)
  const { data: balance, isLoading: isLoadingBalance } = useOutstandingBalance(studentId)

  return (
    <div className="grid grid-cols-1 gap-4 border-t border-line bg-parchment/50 p-4 sm:grid-cols-2">
      <div>
        <p className="mb-2 text-xs font-medium uppercase tracking-wide text-muted">Guardians</p>
        {isLoadingGuardians ? (
          <p className="text-sm text-muted">Loading...</p>
        ) : guardians && guardians.length > 0 ? (
          <ul className="space-y-1.5">
            {guardians.map((guardian) => (
              <li key={guardian.id} className="rounded-md bg-paper px-3 py-2 text-sm text-ink">
                {guardian.fullName} <span className="text-muted">· {guardian.phone}</span>
              </li>
            ))}
          </ul>
        ) : (
          <p className="text-sm text-muted">No other guardians linked.</p>
        )}
      </div>

      <div>
        <p className="mb-2 text-xs font-medium uppercase tracking-wide text-muted">Balance</p>
        {isLoadingBalance ? (
          <p className="text-sm text-muted">Loading...</p>
        ) : (
          <p className="rounded-md bg-paper px-3 py-2 text-sm text-ink">
            Outstanding:{' '}
            <span className="font-medium">
              {currencyFormatter.format(balance?.totalOutstanding ?? 0)}
            </span>
          </p>
        )}
        <p className="mt-2 text-xs text-muted">
          See the Payments and Exams &amp; Grades pages for invoices, report cards, and full grade history.
        </p>
      </div>
    </div>
  )
}

export function MyChildrenPage() {
  const { data: children, isLoading, isError } = useMyChildren()
  const [expandedId, setExpandedId] = useState<string | null>(null)

  return (
    <div>
      <PageHeader title="My Children" description="Your children's profiles, guardians, and balance." />

      {isLoading && <p className="text-sm text-muted">Loading...</p>}
      {isError && <p className="text-sm text-coral">Could not load your children.</p>}

      {children && (
        <div className="overflow-hidden rounded-lg border border-line bg-paper">
          {children.map((child: Student, index) => {
            const isExpanded = expandedId === child.id
            return (
              <div key={child.id} className={index > 0 ? 'border-t border-line' : ''}>
                <div className="flex items-center gap-3 px-4 py-3">
                  <button
                    type="button"
                    onClick={() => setExpandedId(isExpanded ? null : child.id)}
                    aria-label={isExpanded ? 'Collapse' : 'Expand'}
                    className="text-muted transition-colors hover:text-ink"
                  >
                    {isExpanded ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
                  </button>

                  <div className="flex-1">
                    <p className="text-sm font-medium text-ink">{child.fullName}</p>
                    <p className="text-xs text-muted">
                      {child.gender} · born {child.dateOfBirth} · enrolled {child.enrollmentDate}
                    </p>
                  </div>

                  <span
                    className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_CLASSES[child.status]}`}
                  >
                    {child.status}
                  </span>
                </div>

                {isExpanded && <ChildDetails studentId={child.id} />}
              </div>
            )
          })}

          {children.length === 0 && (
            <p className="px-4 py-6 text-center text-sm text-muted">No children linked to your account.</p>
          )}
        </div>
      )}
    </div>
  )
}

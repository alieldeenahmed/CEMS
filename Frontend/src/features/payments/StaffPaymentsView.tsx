import { useState } from 'react'
import { useAuth } from '@/features/auth/AuthContext'
import { ROLES } from '@/features/auth/constants'
import { InvoicesTab } from './InvoicesTab'
import { PackagesTab } from './PackagesTab'

export function StaffPaymentsView() {
  const { hasRole } = useAuth()
  const canManagePackages = hasRole(ROLES.Owner, ROLES.BranchManager)
  const [tab, setTab] = useState<'invoices' | 'packages'>('invoices')

  return (
    <div>
      <div className="mb-5 flex gap-1 border-b border-line">
        <button
          type="button"
          onClick={() => setTab('invoices')}
          className={`px-3 py-2 text-sm font-medium ${
            tab === 'invoices' ? 'border-b-2 border-navy text-navy' : 'text-muted'
          }`}
        >
          Invoices & Payments
        </button>
        {canManagePackages && (
          <button
            type="button"
            onClick={() => setTab('packages')}
            className={`px-3 py-2 text-sm font-medium ${
              tab === 'packages' ? 'border-b-2 border-navy text-navy' : 'text-muted'
            }`}
          >
            Packages
          </button>
        )}
      </div>

      {tab === 'invoices' ? <InvoicesTab /> : <PackagesTab />}
    </div>
  )
}

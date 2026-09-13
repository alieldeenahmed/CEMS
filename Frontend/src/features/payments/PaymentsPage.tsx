import { useAuth } from '@/features/auth/AuthContext'
import { ROLES } from '@/features/auth/constants'
import { PageHeader } from '@/shared/ui/PageHeader'
import { ParentInvoicesView } from './ParentInvoicesView'
import { StaffPaymentsView } from './StaffPaymentsView'

export function PaymentsPage() {
  const { hasRole } = useAuth()
  const isStaff = hasRole(ROLES.Owner, ROLES.BranchManager, ROLES.FrontDesk)

  return (
    <div>
      <PageHeader
        title="Payments"
        description={isStaff ? 'Packages, invoices, and payments.' : "Your children's invoices and balance."}
      />

      {isStaff ? <StaffPaymentsView /> : <ParentInvoicesView />}
    </div>
  )
}

import { PageHeader } from '@/shared/ui/PageHeader'
import { StaffPaymentsView } from './StaffPaymentsView'

export function PaymentsPage() {
  return (
    <div>
      <PageHeader title="Payments" description="Packages, invoices, and payments." />
      <StaffPaymentsView />
    </div>
  )
}

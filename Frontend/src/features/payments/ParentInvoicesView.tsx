import { useState } from 'react'
import { useMyChildren } from '@/features/students/api'
import { Select } from '@/shared/ui/Input'
import { useOutstandingBalance } from './api'
import { InvoiceList } from './InvoiceList'

export function ParentInvoicesView() {
  const { data: children } = useMyChildren()
  const [studentId, setStudentId] = useState('')
  const { data: balance } = useOutstandingBalance(studentId || null)

  return (
    <div>
      <div className="mb-4 flex items-center gap-3">
        <div className="w-64">
          <Select value={studentId} onChange={(e) => setStudentId(e.target.value)}>
            <option value="">Select a child...</option>
            {children?.map((child) => (
              <option key={child.id} value={child.id}>
                {child.fullName}
              </option>
            ))}
          </Select>
        </div>
        {balance && (
          <p className="text-sm text-muted">
            Outstanding balance: <span className="font-medium text-ink">${balance.totalOutstanding}</span>
          </p>
        )}
      </div>

      {!studentId && <p className="text-sm text-muted">Choose a child to see their invoices.</p>}

      {studentId && <InvoiceList studentId={studentId} canRecordPayment={false} canCancel={false} />}
    </div>
  )
}

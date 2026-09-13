import { Plus } from 'lucide-react'
import { useState } from 'react'
import { useAuth } from '@/features/auth/AuthContext'
import { ROLES } from '@/features/auth/constants'
import { useStudents } from '@/features/students/api'
import { Button } from '@/shared/ui/Button'
import { Select } from '@/shared/ui/Input'
import { InvoiceFormModal } from './InvoiceFormModal'
import { InvoiceList } from './InvoiceList'

export function InvoicesTab() {
  const { hasRole } = useAuth()
  const canRecordPayment = hasRole(ROLES.Owner, ROLES.BranchManager, ROLES.FrontDesk)
  const canCancel = hasRole(ROLES.Owner, ROLES.BranchManager)

  const { data: students } = useStudents()
  const [studentId, setStudentId] = useState('')
  const [isCreateOpen, setIsCreateOpen] = useState(false)

  return (
    <div>
      <div className="mb-4 flex items-center gap-3">
        <div className="w-72">
          <Select value={studentId} onChange={(e) => setStudentId(e.target.value)}>
            <option value="">Select a student...</option>
            {students?.map((student) => (
              <option key={student.id} value={student.id}>
                {student.fullName}
              </option>
            ))}
          </Select>
        </div>
        {studentId && (
          <Button onClick={() => setIsCreateOpen(true)}>
            <Plus size={15} />
            New invoice
          </Button>
        )}
      </div>

      {!studentId && <p className="text-sm text-muted">Choose a student to see their invoices.</p>}

      {studentId && (
        <InvoiceList studentId={studentId} canRecordPayment={canRecordPayment} canCancel={canCancel} />
      )}

      {isCreateOpen && studentId && (
        <InvoiceFormModal studentId={studentId} onClose={() => setIsCreateOpen(false)} />
      )}
    </div>
  )
}

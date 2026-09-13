import { Plus } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useStaffUsers } from '@/features/staff/api'
import { Button } from '@/shared/ui/Button'
import { Select } from '@/shared/ui/Input'
import { useStaffPayrollRunsForUser } from './api'
import { GenerateStaffRunModal } from './GenerateStaffRunModal'
import { StaffPayrollRunsList } from './StaffPayrollRunsList'

export function NonTeachingStaffPayrollView() {
  const { data: staff } = useStaffUsers()
  const [userId, setUserId] = useState('')
  const { data: runs, isLoading } = useStaffPayrollRunsForUser(userId || null)
  const [isGenerateOpen, setIsGenerateOpen] = useState(false)

  const eligibleStaff = useMemo(
    () => staff?.filter((u) => u.roles.includes('FrontDesk') || u.roles.includes('BranchManager')) ?? [],
    [staff],
  )

  return (
    <div>
      <div className="mb-4 flex items-center gap-3">
        <div className="w-64">
          <Select value={userId} onChange={(e) => setUserId(e.target.value)}>
            <option value="">Select a staff member...</option>
            {eligibleStaff.map((user) => (
              <option key={user.userId} value={user.userId}>
                {user.fullName} ({user.roles.join(', ')})
              </option>
            ))}
          </Select>
        </div>
        {userId && (
          <Button onClick={() => setIsGenerateOpen(true)}>
            <Plus size={15} />
            Generate run
          </Button>
        )}
      </div>

      {!userId && <p className="text-sm text-muted">Choose a Front Desk or Branch Manager staff member.</p>}
      {userId && isLoading && <p className="text-sm text-muted">Loading payroll runs...</p>}
      {userId && runs && <StaffPayrollRunsList runs={runs} userId={userId} canManage />}

      {isGenerateOpen && userId && (
        <GenerateStaffRunModal userId={userId} onClose={() => setIsGenerateOpen(false)} />
      )}
    </div>
  )
}

import { Plus } from 'lucide-react'
import { useState } from 'react'
import { useBranches } from '@/features/branches/api'
import { Button } from '@/shared/ui/Button'
import { PageHeader } from '@/shared/ui/PageHeader'
import { useSetStaffUserActive, useStaffUsers } from './api'
import { CreateStaffModal } from './CreateStaffModal'

const ROLE_LABELS: Record<string, string> = {
  Owner: 'Owner',
  BranchManager: 'Branch Manager',
  FrontDesk: 'Front Desk',
  Teacher: 'Teacher',
}

export function StaffPage() {
  const { data: staff, isLoading, isError } = useStaffUsers()
  const { data: branches } = useBranches()
  const setActive = useSetStaffUserActive()
  const [isCreateOpen, setIsCreateOpen] = useState(false)

  const branchNameById = new Map(branches?.map((branch) => [branch.id, branch.name]))

  return (
    <div>
      <PageHeader
        title="Staff"
        description="Branch managers, front desk, and teacher accounts."
        action={
          <Button onClick={() => setIsCreateOpen(true)}>
            <Plus size={15} />
            New staff account
          </Button>
        }
      />

      {isLoading && <p className="text-sm text-muted">Loading staff...</p>}
      {isError && <p className="text-sm text-coral">Could not load staff accounts.</p>}

      {staff && (
        <div className="overflow-hidden rounded-lg border border-line bg-paper">
          {staff.map((user, index) => (
            <div
              key={user.userId}
              className={`flex items-center gap-3 px-4 py-3 ${index > 0 ? 'border-t border-line' : ''}`}
            >
              <div className="flex-1">
                <p className="text-sm font-medium text-ink">{user.fullName}</p>
                <p className="text-xs text-muted">{user.email}</p>
              </div>

              <div className="flex flex-wrap gap-1">
                {user.roles.map((role) => (
                  <span
                    key={role}
                    className="rounded-full bg-navy/10 px-2.5 py-0.5 text-xs font-medium text-navy"
                  >
                    {ROLE_LABELS[role] ?? role}
                  </span>
                ))}
              </div>

              <p className="w-40 text-xs text-muted">
                {user.branchIds.length > 0
                  ? user.branchIds.map((id) => branchNameById.get(id) ?? 'Unknown').join(', ')
                  : 'All branches'}
              </p>

              <span
                className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${
                  user.isActive ? 'bg-teal/10 text-teal' : 'bg-muted/10 text-muted'
                }`}
              >
                {user.isActive ? 'Active' : 'Inactive'}
              </span>

              <Button
                variant="secondary"
                onClick={() => setActive.mutate({ userId: user.userId, isActive: !user.isActive })}
              >
                {user.isActive ? 'Deactivate' : 'Activate'}
              </Button>
            </div>
          ))}

          {staff.length === 0 && (
            <p className="px-4 py-6 text-center text-sm text-muted">No staff accounts yet.</p>
          )}
        </div>
      )}

      {isCreateOpen && <CreateStaffModal onClose={() => setIsCreateOpen(false)} />}
    </div>
  )
}

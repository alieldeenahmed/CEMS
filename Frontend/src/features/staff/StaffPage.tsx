import { KeyRound, Plus } from 'lucide-react'
import { useState } from 'react'
import { useAuth } from '@/features/auth/AuthContext'
import { ROLES } from '@/features/auth/constants'
import { useBranches } from '@/features/branches/api'
import { Button } from '@/shared/ui/Button'
import { PageHeader } from '@/shared/ui/PageHeader'
import { SearchInput } from '@/shared/ui/SearchInput'
import { useBranchStaff, useSetStaffUserActive, useStaffUsers } from './api'
import { CreateStaffModal } from './CreateStaffModal'
import { ResetPasswordModal } from './ResetPasswordModal'
import type { StaffUser } from './types'

const ROLE_LABELS: Record<string, string> = {
  Owner: 'Owner',
  BranchManager: 'Branch Manager',
  FrontDesk: 'Front Desk',
  Teacher: 'Teacher',
}

// Only an Owner spans every branch. A teacher's branches live on their teacher profile rather than on
// the account, so "no branch here" must not read as "all branches".
export function branchLabel(staff: StaffUser, branchNameById: Map<string, string>): string {
  if (staff.branchIds.length > 0) {
    return staff.branchIds.map((id) => branchNameById.get(id) ?? 'Unknown').join(', ')
  }
  if (staff.roles.includes('Owner')) return 'All branches'
  if (staff.roles.includes('Teacher')) return 'Set on teacher profile'
  return 'No branch assigned'
}

export function StaffPage() {
  const { user, hasRole } = useAuth()
  const isOwner = hasRole(ROLES.Owner)
  const isBranchManager = hasRole(ROLES.BranchManager)

  const ownerStaffQuery = useStaffUsers({ enabled: isOwner })
  const branchStaffQuery = useBranchStaff({ enabled: isBranchManager && !isOwner })
  const { data: staff, isLoading, isError } = isOwner ? ownerStaffQuery : branchStaffQuery

  const { data: branches } = useBranches()
  const setActive = useSetStaffUserActive()
  const [isCreateOpen, setIsCreateOpen] = useState(false)
  const [resetPasswordUser, setResetPasswordUser] = useState<StaffUser | null>(null)
  const [search, setSearch] = useState('')

  const branchNameById = new Map(branches?.map((branch) => [branch.id, branch.name]))
  const query = search.trim().toLowerCase()
  const filteredStaff = staff?.filter(
    (user) => user.fullName.toLowerCase().includes(query) || user.email.toLowerCase().includes(query),
  )

  return (
    <div>
      <PageHeader
        title="Staff"
        description={
          isOwner
            ? 'Branch managers, front desk, and teacher accounts.'
            : 'Front desk and teacher accounts at your branch.'
        }
        action={
          <Button onClick={() => setIsCreateOpen(true)}>
            <Plus size={15} />
            New staff account
          </Button>
        }
      />

      <SearchInput
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        placeholder="Search by name or email..."
        aria-label="Search staff"
        className="mb-4 max-w-sm"
      />

      {isLoading && <p className="text-sm text-muted">Loading staff...</p>}
      {isError && <p className="text-sm text-coral">Could not load staff accounts.</p>}

      {filteredStaff && (
        <div className="overflow-hidden rounded-lg border border-line bg-paper">
          {filteredStaff.map((staffMember, index) => (
            <div
              key={staffMember.userId}
              className={`flex items-center gap-3 px-4 py-3 ${index > 0 ? 'border-t border-line' : ''}`}
            >
              <div className="flex-1">
                <p className="text-sm font-medium text-ink">{staffMember.fullName}</p>
                <p className="text-xs text-muted">{staffMember.email}</p>
              </div>

              <div className="flex flex-wrap gap-1">
                {staffMember.roles.map((role) => (
                  <span
                    key={role}
                    className="rounded-full bg-navy/10 px-2.5 py-0.5 text-xs font-medium text-navy"
                  >
                    {ROLE_LABELS[role] ?? role}
                  </span>
                ))}
              </div>

              <p className="w-40 text-xs text-muted">
                {branchLabel(staffMember, branchNameById)}
              </p>

              <span
                className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${
                  staffMember.isActive ? 'bg-teal/10 text-teal' : 'bg-muted/10 text-muted'
                }`}
              >
                {staffMember.isActive ? 'Active' : 'Inactive'}
              </span>

              <button
                type="button"
                aria-label={`Reset password for ${staffMember.fullName}`}
                onClick={() => setResetPasswordUser(staffMember)}
                className="text-muted transition-colors hover:text-navy"
              >
                <KeyRound size={15} />
              </button>

              {isOwner && (
                <Button
                  variant="secondary"
                  onClick={() => setActive.mutate({ userId: staffMember.userId, isActive: !staffMember.isActive })}
                >
                  {staffMember.isActive ? 'Deactivate' : 'Activate'}
                </Button>
              )}
            </div>
          ))}

          {filteredStaff.length === 0 && (
            <p className="px-4 py-6 text-center text-sm text-muted">
              {search ? 'No staff accounts match your search.' : 'No staff accounts yet.'}
            </p>
          )}
        </div>
      )}

      {isCreateOpen &&
        (isOwner ? (
          <CreateStaffModal onClose={() => setIsCreateOpen(false)} />
        ) : (
          <CreateStaffModal
            onClose={() => setIsCreateOpen(false)}
            allowedRoles={['FrontDesk', 'Teacher']}
            lockedBranchId={user?.branchIds[0]}
          />
        ))}
      {resetPasswordUser && (
        <ResetPasswordModal user={resetPasswordUser} onClose={() => setResetPasswordUser(null)} />
      )}
    </div>
  )
}

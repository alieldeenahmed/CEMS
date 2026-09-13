import { X } from 'lucide-react'
import { useState } from 'react'
import { useBranches } from '@/features/branches/api'
import { Button } from '@/shared/ui/Button'
import { useAddTeacherToBranch, useRemoveTeacherFromBranch } from './api'
import type { Teacher } from './types'

export function BranchAssignmentsSection({ teacher }: { teacher: Teacher }) {
  const { data: branches } = useBranches()
  const addToBranch = useAddTeacherToBranch(teacher.id)
  const removeFromBranch = useRemoveTeacherFromBranch(teacher.id)
  const [selectedBranchId, setSelectedBranchId] = useState('')

  const assignedBranches = branches?.filter((b) => teacher.branchIds.includes(b.id)) ?? []
  const availableBranches = branches?.filter((b) => !teacher.branchIds.includes(b.id)) ?? []

  return (
    <div>
      <p className="mb-2 text-xs font-medium uppercase tracking-wide text-muted">Branches</p>

      <div className="mb-2 flex flex-wrap gap-1.5">
        {assignedBranches.map((branch) => (
          <span
            key={branch.id}
            className="flex items-center gap-1 rounded-full bg-navy/10 px-2.5 py-0.5 text-xs font-medium text-navy"
          >
            {branch.name}
            <button
              type="button"
              aria-label={`Remove from ${branch.name}`}
              onClick={() => removeFromBranch.mutate(branch.id)}
              className="hover:text-coral"
            >
              <X size={12} />
            </button>
          </span>
        ))}
        {assignedBranches.length === 0 && <p className="text-sm text-muted">Not assigned to any branch.</p>}
      </div>

      {availableBranches.length > 0 && (
        <div className="flex items-center gap-2">
          <select
            value={selectedBranchId}
            onChange={(e) => setSelectedBranchId(e.target.value)}
            className="rounded-md border border-line bg-paper px-2 py-1.5 text-sm outline-none focus:border-navy"
          >
            <option value="">Add to branch...</option>
            {availableBranches.map((branch) => (
              <option key={branch.id} value={branch.id}>
                {branch.name}
              </option>
            ))}
          </select>
          <Button
            variant="secondary"
            className="px-2.5 py-1.5 text-xs"
            disabled={!selectedBranchId}
            onClick={() => {
              addToBranch.mutate(selectedBranchId)
              setSelectedBranchId('')
            }}
          >
            Add
          </Button>
        </div>
      )}
    </div>
  )
}

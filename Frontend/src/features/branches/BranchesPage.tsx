import { ChevronDown, ChevronRight, Pencil, Plus } from 'lucide-react'
import { useState } from 'react'
import { PageHeader } from '@/shared/ui/PageHeader'
import { Button } from '@/shared/ui/Button'
import { useBranches, useUpdateBranch } from './api'
import { BranchFormModal } from './BranchFormModal'
import { RoomsSection } from './RoomsSection'
import type { Branch } from './types'

export function BranchesPage() {
  const { data: branches, isLoading, isError } = useBranches()
  const updateBranch = useUpdateBranch()
  const [expandedBranchId, setExpandedBranchId] = useState<string | null>(null)
  const [modalState, setModalState] = useState<'closed' | 'create' | Branch>('closed')

  function toggleActive(branch: Branch) {
    updateBranch.mutate({
      id: branch.id,
      name: branch.name,
      address: branch.address,
      phone: branch.phone,
      isActive: !branch.isActive,
    })
  }

  return (
    <div>
      <PageHeader
        title="Branches"
        description="Manage branch locations and their rooms."
        action={
          <Button onClick={() => setModalState('create')}>
            <Plus size={15} />
            New branch
          </Button>
        }
      />

      {isLoading && <p className="text-sm text-muted">Loading branches...</p>}
      {isError && <p className="text-sm text-coral">Could not load branches.</p>}

      {branches && (
        <div className="overflow-hidden rounded-lg border border-line bg-paper">
          {branches.map((branch, index) => {
            const isExpanded = expandedBranchId === branch.id
            return (
              <div key={branch.id} className={index > 0 ? 'border-t border-line' : ''}>
                <div className="flex items-center gap-3 px-4 py-3">
                  <button
                    type="button"
                    onClick={() => setExpandedBranchId(isExpanded ? null : branch.id)}
                    aria-label={isExpanded ? 'Collapse' : 'Expand'}
                    className="text-muted transition-colors hover:text-ink"
                  >
                    {isExpanded ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
                  </button>

                  <div className="flex-1">
                    <p className="text-sm font-medium text-ink">{branch.name}</p>
                    <p className="text-xs text-muted">
                      {branch.address} · {branch.phone}
                    </p>
                  </div>

                  <span
                    className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${
                      branch.isActive
                        ? 'bg-teal/10 text-teal'
                        : 'bg-muted/10 text-muted'
                    }`}
                  >
                    {branch.isActive ? 'Active' : 'Inactive'}
                  </span>

                  <Button variant="secondary" onClick={() => toggleActive(branch)}>
                    {branch.isActive ? 'Deactivate' : 'Activate'}
                  </Button>

                  <button
                    type="button"
                    aria-label={`Edit ${branch.name}`}
                    onClick={() => setModalState(branch)}
                    className="text-muted transition-colors hover:text-navy"
                  >
                    <Pencil size={15} />
                  </button>
                </div>

                {isExpanded && <RoomsSection branchId={branch.id} />}
              </div>
            )
          })}

          {branches.length === 0 && (
            <p className="px-4 py-6 text-center text-sm text-muted">No branches yet.</p>
          )}
        </div>
      )}

      {modalState !== 'closed' && (
        <BranchFormModal
          branch={modalState === 'create' ? null : modalState}
          onClose={() => setModalState('closed')}
        />
      )}
    </div>
  )
}

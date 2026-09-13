import { ChevronDown, ChevronRight, Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useAuth } from '@/features/auth/AuthContext'
import { ROLES } from '@/features/auth/constants'
import { Button } from '@/shared/ui/Button'
import { PageHeader } from '@/shared/ui/PageHeader'
import { SearchInput } from '@/shared/ui/SearchInput'
import { getErrorMessage } from '@/shared/api/errors'
import { useCurricula, useDeleteCurriculum } from './api'
import { CurriculumFormModal } from './CurriculumFormModal'
import { SubjectsSection } from './SubjectsSection'
import type { Curriculum } from './types'

export function CurriculaPage() {
  const { hasRole } = useAuth()
  const canManage = hasRole(ROLES.Owner, ROLES.BranchManager)
  const { data: curricula, isLoading, isError } = useCurricula()
  const deleteCurriculum = useDeleteCurriculum()
  const [expandedId, setExpandedId] = useState<string | null>(null)
  const [modalState, setModalState] = useState<'closed' | 'create' | Curriculum>('closed')
  const [search, setSearch] = useState('')

  const query = search.trim().toLowerCase()
  const filteredCurricula = curricula?.filter(
    (curriculum) =>
      curriculum.name.toLowerCase().includes(query) || curriculum.description.toLowerCase().includes(query),
  )

  function handleDelete(curriculum: Curriculum) {
    if (window.confirm(`Delete "${curriculum.name}"? This cannot be undone.`)) {
      deleteCurriculum.mutate(curriculum.id, {
        onError: (error) => window.alert(getErrorMessage(error, 'Could not delete this curriculum.')),
      })
    }
  }

  return (
    <div>
      <PageHeader
        title="Curricula"
        description="Curricula and the subjects taught under each one."
        action={
          canManage ? (
            <Button onClick={() => setModalState('create')}>
              <Plus size={15} />
              New curriculum
            </Button>
          ) : undefined
        }
      />

      <SearchInput
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        placeholder="Search by name or description..."
        aria-label="Search curricula"
        className="mb-4 max-w-sm"
      />

      {isLoading && <p className="text-sm text-muted">Loading curricula...</p>}
      {isError && <p className="text-sm text-coral">Could not load curricula.</p>}

      {filteredCurricula && (
        <div className="overflow-hidden rounded-lg border border-line bg-paper">
          {filteredCurricula.map((curriculum, index) => {
            const isExpanded = expandedId === curriculum.id
            return (
              <div key={curriculum.id} className={index > 0 ? 'border-t border-line' : ''}>
                <div className="flex items-center gap-3 px-4 py-3">
                  <button
                    type="button"
                    onClick={() => setExpandedId(isExpanded ? null : curriculum.id)}
                    aria-label={isExpanded ? 'Collapse' : 'Expand'}
                    className="text-muted transition-colors hover:text-ink"
                  >
                    {isExpanded ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
                  </button>

                  <div className="flex-1">
                    <p className="text-sm font-medium text-ink">{curriculum.name}</p>
                    <p className="text-xs text-muted">{curriculum.description}</p>
                  </div>

                  {canManage && (
                    <>
                      <button
                        type="button"
                        aria-label={`Edit ${curriculum.name}`}
                        onClick={() => setModalState(curriculum)}
                        className="text-muted transition-colors hover:text-navy"
                      >
                        <Pencil size={15} />
                      </button>
                      <button
                        type="button"
                        aria-label={`Delete ${curriculum.name}`}
                        onClick={() => handleDelete(curriculum)}
                        className="text-muted transition-colors hover:text-coral"
                      >
                        <Trash2 size={15} />
                      </button>
                    </>
                  )}
                </div>

                {isExpanded && <SubjectsSection curriculumId={curriculum.id} />}
              </div>
            )
          })}

          {filteredCurricula.length === 0 && (
            <p className="px-4 py-6 text-center text-sm text-muted">
              {search ? 'No curricula match your search.' : 'No curricula yet.'}
            </p>
          )}
        </div>
      )}

      {modalState !== 'closed' && (
        <CurriculumFormModal
          curriculum={modalState === 'create' ? null : modalState}
          onClose={() => setModalState('closed')}
        />
      )}
    </div>
  )
}

import { ChevronDown, ChevronRight, Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useAuth } from '@/features/auth/AuthContext'
import { ROLES } from '@/features/auth/constants'
import { Button } from '@/shared/ui/Button'
import { PageHeader } from '@/shared/ui/PageHeader'
import { SearchInput } from '@/shared/ui/SearchInput'
import { AvailabilitySection } from './AvailabilitySection'
import { useDeleteTeacher, useTeachers } from './api'
import { BranchAssignmentsSection } from './BranchAssignmentsSection'
import { TeacherFormModal } from './TeacherFormModal'
import type { Teacher } from './types'

export function TeachersPage() {
  const { hasRole } = useAuth()
  const isOwner = hasRole(ROLES.Owner)
  const canCreateProfile = hasRole(ROLES.Owner, ROLES.BranchManager)
  const { data: teachers, isLoading, isError } = useTeachers()
  const deleteTeacher = useDeleteTeacher()
  const [expandedTeacherId, setExpandedTeacherId] = useState<string | null>(null)
  const [modalState, setModalState] = useState<'closed' | 'create' | Teacher>('closed')
  const [search, setSearch] = useState('')

  const query = search.trim().toLowerCase()
  const filteredTeachers = teachers?.filter(
    (teacher) => teacher.fullName.toLowerCase().includes(query) || teacher.email.toLowerCase().includes(query),
  )

  function handleDelete(teacher: Teacher) {
    if (window.confirm(`Remove ${teacher.fullName}'s teacher profile? This cannot be undone.`)) {
      deleteTeacher.mutate(teacher.id)
    }
  }

  return (
    <div>
      <PageHeader
        title="Teachers"
        description="Teacher profiles, branch assignments, and availability."
        action={
          canCreateProfile ? (
            <Button onClick={() => setModalState('create')}>
              <Plus size={15} />
              New teacher profile
            </Button>
          ) : undefined
        }
      />

      <SearchInput
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        placeholder="Search by name or email..."
        aria-label="Search teachers"
        className="mb-4 max-w-sm"
      />

      {isLoading && <p className="text-sm text-muted">Loading teachers...</p>}
      {isError && <p className="text-sm text-coral">Could not load teachers.</p>}

      {filteredTeachers && (
        <div className="overflow-hidden rounded-lg border border-line bg-paper">
          {filteredTeachers.map((teacher, index) => {
            const isExpanded = expandedTeacherId === teacher.id
            return (
              <div key={teacher.id} className={index > 0 ? 'border-t border-line' : ''}>
                <div className="flex items-center gap-3 px-4 py-3">
                  <button
                    type="button"
                    onClick={() => setExpandedTeacherId(isExpanded ? null : teacher.id)}
                    aria-label={isExpanded ? 'Collapse' : 'Expand'}
                    className="text-muted transition-colors hover:text-ink"
                  >
                    {isExpanded ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
                  </button>

                  <div className="flex-1">
                    <p className="text-sm font-medium text-ink">{teacher.fullName}</p>
                    <p className="text-xs text-muted">
                      {teacher.email} · hired {teacher.hireDate}
                    </p>
                  </div>

                  <span className="rounded-full bg-ochre/10 px-2.5 py-0.5 text-xs font-medium text-ochre">
                    {teacher.payType === 'PerSession' ? 'Per Session' : 'Hourly'} · {teacher.payRate}
                  </span>

                  {isOwner && (
                    <>
                      <button
                        type="button"
                        aria-label={`Edit ${teacher.fullName}`}
                        onClick={() => setModalState(teacher)}
                        className="text-muted transition-colors hover:text-navy"
                      >
                        <Pencil size={15} />
                      </button>
                      <button
                        type="button"
                        aria-label={`Delete ${teacher.fullName}`}
                        onClick={() => handleDelete(teacher)}
                        className="text-muted transition-colors hover:text-coral"
                      >
                        <Trash2 size={15} />
                      </button>
                    </>
                  )}
                </div>

                {isExpanded && (
                  <div className="space-y-4 border-t border-line bg-parchment/50 p-4">
                    <BranchAssignmentsSection teacher={teacher} />
                    <AvailabilitySection teacherId={teacher.id} />
                  </div>
                )}
              </div>
            )
          })}

          {filteredTeachers.length === 0 && (
            <p className="px-4 py-6 text-center text-sm text-muted">
              {search ? 'No teachers match your search.' : 'No teacher profiles yet.'}
            </p>
          )}
        </div>
      )}

      {modalState !== 'closed' && (
        <TeacherFormModal
          teacher={modalState === 'create' ? null : modalState}
          onClose={() => setModalState('closed')}
        />
      )}
    </div>
  )
}

import { ChevronRight, Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useBranches } from '@/features/branches/api'
import { Button } from '@/shared/ui/Button'
import { PageHeader } from '@/shared/ui/PageHeader'
import { SearchInput } from '@/shared/ui/SearchInput'
import { useDeleteStudent, useStudents } from './api'
import { StudentFormModal } from './StudentFormModal'
import type { Student } from './types'

const STATUS_CLASSES: Record<string, string> = {
  Active: 'bg-teal/10 text-teal',
  Paused: 'bg-ochre/10 text-ochre',
  Graduated: 'bg-muted/10 text-muted',
}

export function StudentsPage() {
  const navigate = useNavigate()
  const { data: students, isLoading, isError } = useStudents()
  const { data: branches } = useBranches()
  const deleteStudent = useDeleteStudent()
  const [modalState, setModalState] = useState<'closed' | 'create' | Student>('closed')
  const [search, setSearch] = useState('')

  const branchNameById = new Map(branches?.map((branch) => [branch.id, branch.name]))
  const query = search.trim().toLowerCase()
  const filteredStudents = students?.filter((student) => student.fullName.toLowerCase().includes(query))

  function handleDelete(student: Student) {
    if (window.confirm(`Remove ${student.fullName}? This cannot be undone.`)) {
      deleteStudent.mutate(student.id)
    }
  }

  return (
    <div>
      <PageHeader
        title="Students"
        description="Manage student records and their linked guardians."
        action={
          <Button onClick={() => setModalState('create')}>
            <Plus size={15} />
            New student
          </Button>
        }
      />

      <SearchInput
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        placeholder="Search by name..."
        aria-label="Search students"
        className="mb-4 max-w-sm"
      />

      {isLoading && <p className="text-sm text-muted">Loading students...</p>}
      {isError && <p className="text-sm text-coral">Could not load students.</p>}

      {filteredStudents && (
        <div className="overflow-hidden rounded-lg border border-line bg-paper">
          {filteredStudents.map((student, index) => (
            <div
              key={student.id}
              onClick={() => navigate(`/students/${student.id}`)}
              className={`flex cursor-pointer items-center gap-3 px-4 py-3 transition-colors hover:bg-parchment/50 ${index > 0 ? 'border-t border-line' : ''}`}
            >
              <ChevronRight size={16} className="text-muted" />

              <div className="flex-1">
                <p className="text-sm font-medium text-ink">{student.fullName}</p>
                <p className="text-xs text-muted">
                  {student.gender} · born {student.dateOfBirth} ·{' '}
                  {branchNameById.get(student.currentBranchId) ?? 'Unknown branch'}
                </p>
              </div>

              <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_CLASSES[student.status]}`}>
                {student.status}
              </span>

              <button
                type="button"
                aria-label={`Edit ${student.fullName}`}
                onClick={(e) => {
                  e.stopPropagation()
                  setModalState(student)
                }}
                className="text-muted transition-colors hover:text-navy"
              >
                <Pencil size={15} />
              </button>

              <button
                type="button"
                aria-label={`Delete ${student.fullName}`}
                onClick={(e) => {
                  e.stopPropagation()
                  handleDelete(student)
                }}
                className="text-muted transition-colors hover:text-coral"
              >
                <Trash2 size={15} />
              </button>
            </div>
          ))}

          {filteredStudents.length === 0 && (
            <p className="px-4 py-6 text-center text-sm text-muted">
              {search ? 'No students match your search.' : 'No students yet.'}
            </p>
          )}
        </div>
      )}

      {modalState !== 'closed' && (
        <StudentFormModal
          student={modalState === 'create' ? null : modalState}
          onClose={() => setModalState('closed')}
        />
      )}
    </div>
  )
}

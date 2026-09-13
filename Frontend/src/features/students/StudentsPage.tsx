import { ChevronDown, ChevronRight, Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useBranches } from '@/features/branches/api'
import { Button } from '@/shared/ui/Button'
import { PageHeader } from '@/shared/ui/PageHeader'
import { useDeleteStudent, useStudents } from './api'
import { GuardiansSection } from './GuardiansSection'
import { StudentFormModal } from './StudentFormModal'
import type { Student } from './types'

const STATUS_CLASSES: Record<string, string> = {
  Active: 'bg-teal/10 text-teal',
  Paused: 'bg-ochre/10 text-ochre',
  Graduated: 'bg-muted/10 text-muted',
}

export function StudentsPage() {
  const { data: students, isLoading, isError } = useStudents()
  const { data: branches } = useBranches()
  const deleteStudent = useDeleteStudent()
  const [expandedStudentId, setExpandedStudentId] = useState<string | null>(null)
  const [modalState, setModalState] = useState<'closed' | 'create' | Student>('closed')

  const branchNameById = new Map(branches?.map((branch) => [branch.id, branch.name]))

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

      {isLoading && <p className="text-sm text-muted">Loading students...</p>}
      {isError && <p className="text-sm text-coral">Could not load students.</p>}

      {students && (
        <div className="overflow-hidden rounded-lg border border-line bg-paper">
          {students.map((student, index) => {
            const isExpanded = expandedStudentId === student.id
            return (
              <div key={student.id} className={index > 0 ? 'border-t border-line' : ''}>
                <div className="flex items-center gap-3 px-4 py-3">
                  <button
                    type="button"
                    onClick={() => setExpandedStudentId(isExpanded ? null : student.id)}
                    aria-label={isExpanded ? 'Collapse' : 'Expand'}
                    className="text-muted transition-colors hover:text-ink"
                  >
                    {isExpanded ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
                  </button>

                  <div className="flex-1">
                    <p className="text-sm font-medium text-ink">{student.fullName}</p>
                    <p className="text-xs text-muted">
                      {student.gender} · born {student.dateOfBirth} ·{' '}
                      {branchNameById.get(student.currentBranchId) ?? 'Unknown branch'}
                    </p>
                  </div>

                  <span
                    className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_CLASSES[student.status]}`}
                  >
                    {student.status}
                  </span>

                  <button
                    type="button"
                    aria-label={`Edit ${student.fullName}`}
                    onClick={() => setModalState(student)}
                    className="text-muted transition-colors hover:text-navy"
                  >
                    <Pencil size={15} />
                  </button>

                  <button
                    type="button"
                    aria-label={`Delete ${student.fullName}`}
                    onClick={() => handleDelete(student)}
                    className="text-muted transition-colors hover:text-coral"
                  >
                    <Trash2 size={15} />
                  </button>
                </div>

                {isExpanded && <GuardiansSection studentId={student.id} />}
              </div>
            )
          })}

          {students.length === 0 && (
            <p className="px-4 py-6 text-center text-sm text-muted">No students yet.</p>
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

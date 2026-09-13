import { Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Button } from '@/shared/ui/Button'
import { Input } from '@/shared/ui/Input'
import { getErrorMessage } from '@/shared/api/errors'
import { useCreateSubject, useDeleteSubject, useSubjects, useUpdateSubject } from './api'

export function SubjectsSection({ curriculumId }: { curriculumId: string }) {
  const { data: subjects, isLoading } = useSubjects(curriculumId)
  const createSubject = useCreateSubject()
  const updateSubject = useUpdateSubject()
  const deleteSubject = useDeleteSubject()
  const [editingSubjectId, setEditingSubjectId] = useState<string | null>(null)

  const { register, handleSubmit, reset } = useForm<{ name: string }>({ defaultValues: { name: '' } })

  async function onAddSubject(values: { name: string }) {
    await createSubject.mutateAsync({ name: values.name, curriculumId })
    reset()
  }

  if (isLoading) {
    return <p className="text-sm text-muted">Loading subjects...</p>
  }

  return (
    <div className="border-t border-line bg-parchment/50 p-4">
      <p className="mb-3 text-xs font-medium uppercase tracking-wide text-muted">Subjects</p>

      {subjects && subjects.length > 0 ? (
        <ul className="mb-4 space-y-1.5">
          {subjects.map((subject) => (
            <li
              key={subject.id}
              className="flex items-center justify-between rounded-md bg-paper px-3 py-2 text-sm"
            >
              {editingSubjectId === subject.id ? (
                <SubjectEditRow
                  initialName={subject.name}
                  onCancel={() => setEditingSubjectId(null)}
                  onSave={async (name) => {
                    await updateSubject.mutateAsync({ id: subject.id, name })
                    setEditingSubjectId(null)
                  }}
                />
              ) : (
                <>
                  <button
                    type="button"
                    className="flex-1 text-left text-ink"
                    onClick={() => setEditingSubjectId(subject.id)}
                  >
                    {subject.name}
                  </button>
                  <button
                    type="button"
                    aria-label={`Delete ${subject.name}`}
                    onClick={() =>
                      deleteSubject.mutate(subject.id, {
                        onError: (error) =>
                          window.alert(getErrorMessage(error, 'Could not delete this subject.')),
                      })
                    }
                    className="text-muted transition-colors hover:text-coral"
                  >
                    <Trash2 size={15} />
                  </button>
                </>
              )}
            </li>
          ))}
        </ul>
      ) : (
        <p className="mb-4 text-sm text-muted">No subjects yet.</p>
      )}

      <form onSubmit={handleSubmit(onAddSubject)} className="flex items-end gap-2">
        <div className="flex-1">
          <Input placeholder="Subject name" {...register('name', { required: true })} />
        </div>
        <Button type="submit" variant="secondary" disabled={createSubject.isPending}>
          <Plus size={15} />
          Add
        </Button>
      </form>
    </div>
  )
}

function SubjectEditRow({
  initialName,
  onSave,
  onCancel,
}: {
  initialName: string
  onSave: (name: string) => void
  onCancel: () => void
}) {
  const { register, handleSubmit } = useForm<{ name: string }>({ defaultValues: { name: initialName } })

  return (
    <form
      onSubmit={handleSubmit((values) => onSave(values.name))}
      className="flex w-full items-center gap-2"
    >
      <input
        className="flex-1 rounded-md border border-line px-2 py-1 text-sm outline-none focus:border-navy"
        {...register('name', { required: true })}
      />
      <Button type="submit" variant="secondary" className="px-2 py-1 text-xs">
        Save
      </Button>
      <Button type="button" variant="ghost" className="px-2 py-1 text-xs" onClick={onCancel}>
        Cancel
      </Button>
    </form>
  )
}

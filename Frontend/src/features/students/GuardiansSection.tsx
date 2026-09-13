import { Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Button } from '@/shared/ui/Button'
import { Input, Select } from '@/shared/ui/Input'
import {
  useAllGuardians,
  useCreateGuardian,
  useGuardiansForStudent,
  useLinkGuardian,
  useUnlinkGuardian,
} from './api'
import type { RelationshipType } from './types'

interface LinkFormValues {
  mode: 'existing' | 'new'
  guardianId: string
  fullName: string
  phone: string
  email: string
  relationshipType: RelationshipType
  isPrimaryContact: boolean
}

export function GuardiansSection({ studentId }: { studentId: string }) {
  const { data: guardians, isLoading } = useGuardiansForStudent(studentId)
  const { data: allGuardians } = useAllGuardians()
  const createGuardian = useCreateGuardian()
  const linkGuardian = useLinkGuardian(studentId)
  const unlinkGuardian = useUnlinkGuardian(studentId)
  const [isFormOpen, setIsFormOpen] = useState(false)

  const linkedIds = new Set(guardians?.map((g) => g.id))
  const availableGuardians = allGuardians?.filter((g) => !linkedIds.has(g.id)) ?? []

  const { register, handleSubmit, watch, reset } = useForm<LinkFormValues>({
    defaultValues: {
      mode: 'existing',
      relationshipType: 'Guardian',
      isPrimaryContact: false,
    },
  })

  const mode = watch('mode')

  async function onSubmit(values: LinkFormValues) {
    let guardianId = values.guardianId

    if (values.mode === 'new') {
      const guardian = await createGuardian.mutateAsync({
        fullName: values.fullName,
        phone: values.phone,
        email: values.email,
      })
      guardianId = guardian.id
    }

    if (!guardianId) return

    await linkGuardian.mutateAsync({
      guardianId,
      relationshipType: values.relationshipType,
      isPrimaryContact: values.isPrimaryContact,
    })
    reset({ mode: 'existing', relationshipType: 'Guardian', isPrimaryContact: false })
    setIsFormOpen(false)
  }

  if (isLoading) {
    return <p className="text-sm text-muted">Loading guardians...</p>
  }

  return (
    <div className="border-t border-line bg-parchment/50 p-4">
      <p className="mb-3 text-xs font-medium uppercase tracking-wide text-muted">Guardians</p>

      {guardians && guardians.length > 0 ? (
        <ul className="mb-4 space-y-1.5">
          {guardians.map((guardian) => (
            <li
              key={guardian.id}
              className="flex items-center justify-between rounded-md bg-paper px-3 py-2 text-sm"
            >
              <span className="text-ink">
                {guardian.fullName}{' '}
                <span className="text-muted">
                  · {guardian.phone} · {guardian.email}
                </span>
              </span>
              <button
                type="button"
                aria-label={`Unlink ${guardian.fullName}`}
                onClick={() => unlinkGuardian.mutate(guardian.id)}
                className="text-muted transition-colors hover:text-coral"
              >
                <Trash2 size={15} />
              </button>
            </li>
          ))}
        </ul>
      ) : (
        <p className="mb-4 text-sm text-muted">No guardians linked yet.</p>
      )}

      {isFormOpen ? (
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-3 rounded-md bg-paper p-3">
          <div className="flex gap-4 text-sm">
            <label className="flex items-center gap-1.5">
              <input type="radio" value="existing" {...register('mode')} /> Existing guardian
            </label>
            <label className="flex items-center gap-1.5">
              <input type="radio" value="new" {...register('mode')} /> New guardian
            </label>
          </div>

          {mode === 'existing' ? (
            <Select {...register('guardianId')}>
              <option value="">Select a guardian</option>
              {availableGuardians.map((guardian) => (
                <option key={guardian.id} value={guardian.id}>
                  {guardian.fullName} ({guardian.email})
                </option>
              ))}
            </Select>
          ) : (
            <div className="grid grid-cols-3 gap-2">
              <Input placeholder="Full name" {...register('fullName', { required: true })} />
              <Input placeholder="Phone" {...register('phone', { required: true })} />
              <Input placeholder="Email" type="email" {...register('email', { required: true })} />
            </div>
          )}

          <div className="flex items-center gap-4">
            <Select {...register('relationshipType')} className="w-40">
              <option value="Mother">Mother</option>
              <option value="Father">Father</option>
              <option value="Guardian">Guardian</option>
            </Select>
            <label className="flex items-center gap-1.5 text-sm text-ink">
              <input type="checkbox" {...register('isPrimaryContact')} /> Primary contact
            </label>
          </div>

          <div className="flex justify-end gap-2">
            <Button type="button" variant="ghost" onClick={() => setIsFormOpen(false)}>
              Cancel
            </Button>
            <Button type="submit" variant="secondary" disabled={linkGuardian.isPending || createGuardian.isPending}>
              Link guardian
            </Button>
          </div>
        </form>
      ) : (
        <Button variant="secondary" onClick={() => setIsFormOpen(true)}>
          Link a guardian
        </Button>
      )}
    </div>
  )
}

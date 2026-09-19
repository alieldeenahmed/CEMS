import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { getErrorMessage } from '@/shared/api/errors'
import { Button } from '@/shared/ui/Button'
import { Input } from '@/shared/ui/Input'
import { Modal } from '@/shared/ui/Modal'
import { useCreateCurriculum, useUpdateCurriculum } from './api'
import type { Curriculum } from './types'

const curriculumSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  description: z.string().min(1, 'Description is required'),
})

type CurriculumFormValues = z.infer<typeof curriculumSchema>

export function CurriculumFormModal({
  curriculum,
  onClose,
}: {
  curriculum: Curriculum | null
  onClose: () => void
}) {
  const createCurriculum = useCreateCurriculum()
  const updateCurriculum = useUpdateCurriculum()

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<CurriculumFormValues>({
    resolver: zodResolver(curriculumSchema),
    defaultValues: {
      name: curriculum?.name ?? '',
      description: curriculum?.description ?? '',
    },
  })

  const [serverError, setServerError] = useState<string | null>(null)
  const isSaving = createCurriculum.isPending || updateCurriculum.isPending

  async function onSubmit(values: CurriculumFormValues) {
    setServerError(null)
    try {
      if (curriculum) {
        await updateCurriculum.mutateAsync({ id: curriculum.id, ...values })
      } else {
        await createCurriculum.mutateAsync(values)
      }
      onClose()
    } catch (error) {
      setServerError(getErrorMessage(error, 'Could not save this curriculum.'))
    }
  }

  return (
    <Modal title={curriculum ? 'Edit curriculum' : 'New curriculum'} onClose={onClose}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <Input label="Name" {...register('name')} error={errors.name?.message} />
        <Input label="Description" {...register('description')} error={errors.description?.message} />

        {serverError && <p className="text-sm text-coral">{serverError}</p>}

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={isSaving}>
            {isSaving ? 'Saving...' : 'Save'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}

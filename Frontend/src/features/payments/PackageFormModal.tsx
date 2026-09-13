import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Button } from '@/shared/ui/Button'
import { Input } from '@/shared/ui/Input'
import { Modal } from '@/shared/ui/Modal'
import { useCreatePackage, useUpdatePackage } from './api'
import type { Package } from './types'

const packageSchema = z.object({
  sessionCount: z.number().int().positive('Session count must be greater than 0'),
  price: z.number().positive('Price must be greater than 0'),
})

type PackageFormValues = z.infer<typeof packageSchema>

export function PackageFormModal({
  courseId,
  pkg,
  onClose,
}: {
  courseId: string
  pkg: Package | null
  onClose: () => void
}) {
  const createPackage = useCreatePackage(courseId)
  const updatePackage = useUpdatePackage(courseId)

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<PackageFormValues>({
    resolver: zodResolver(packageSchema),
    defaultValues: {
      sessionCount: pkg?.sessionCount ?? 1,
      price: pkg?.price ?? 0,
    },
  })

  const isSaving = createPackage.isPending || updatePackage.isPending

  async function onSubmit(values: PackageFormValues) {
    if (pkg) {
      await updatePackage.mutateAsync({ id: pkg.id, ...values })
    } else {
      await createPackage.mutateAsync(values)
    }
    onClose()
  }

  return (
    <Modal title={pkg ? 'Edit package' : 'New package'} onClose={onClose}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <Input
          label="Session count"
          type="number"
          {...register('sessionCount', { valueAsNumber: true })}
          error={errors.sessionCount?.message}
        />
        <Input
          label="Price"
          type="number"
          step="0.01"
          {...register('price', { valueAsNumber: true })}
          error={errors.price?.message}
        />

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

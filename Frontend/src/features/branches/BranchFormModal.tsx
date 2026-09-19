import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { getErrorMessage } from '@/shared/api/errors'
import { Button } from '@/shared/ui/Button'
import { Input } from '@/shared/ui/Input'
import { Modal } from '@/shared/ui/Modal'
import { useCreateBranch, useUpdateBranch } from './api'
import type { Branch } from './types'

const branchSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  address: z.string().min(1, 'Address is required'),
  phone: z.string().min(1, 'Phone is required'),
})

type BranchFormValues = z.infer<typeof branchSchema>

interface BranchFormModalProps {
  branch: Branch | null
  onClose: () => void
}

export function BranchFormModal({ branch, onClose }: BranchFormModalProps) {
  const createBranch = useCreateBranch()
  const updateBranch = useUpdateBranch()

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<BranchFormValues>({
    resolver: zodResolver(branchSchema),
    defaultValues: {
      name: branch?.name ?? '',
      address: branch?.address ?? '',
      phone: branch?.phone ?? '',
    },
  })

  const [serverError, setServerError] = useState<string | null>(null)
  const isSaving = createBranch.isPending || updateBranch.isPending

  async function onSubmit(values: BranchFormValues) {
    setServerError(null)
    try {
      if (branch) {
        await updateBranch.mutateAsync({ id: branch.id, isActive: branch.isActive, ...values })
      } else {
        await createBranch.mutateAsync(values)
      }
      onClose()
    } catch (error) {
      setServerError(getErrorMessage(error, 'Could not save this branch.'))
    }
  }

  return (
    <Modal title={branch ? 'Edit branch' : 'New branch'} onClose={onClose}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <Input label="Name" {...register('name')} error={errors.name?.message} />
        <Input label="Address" {...register('address')} error={errors.address?.message} />
        <Input label="Phone" {...register('phone')} error={errors.phone?.message} />

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

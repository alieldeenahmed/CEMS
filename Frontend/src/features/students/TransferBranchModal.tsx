import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { useBranches } from '@/features/branches/api'
import { getErrorMessage } from '@/shared/api/errors'
import { Button } from '@/shared/ui/Button'
import { Input, Select } from '@/shared/ui/Input'
import { Modal } from '@/shared/ui/Modal'
import { useTransferStudentBranch } from './api'
import type { Student } from './types'

const transferSchema = z.object({
  newBranchId: z.string().min(1, 'Select a branch'),
  reason: z.string().max(500).optional(),
})

type TransferFormValues = z.infer<typeof transferSchema>

export function TransferBranchModal({ student, onClose }: { student: Student; onClose: () => void }) {
  const { data: branches } = useBranches()
  const transferBranch = useTransferStudentBranch(student.id)
  const [serverError, setServerError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<TransferFormValues>({ resolver: zodResolver(transferSchema) })

  const otherBranches = branches?.filter((branch) => branch.id !== student.currentBranchId) ?? []

  async function onSubmit(values: TransferFormValues) {
    setServerError(null)
    try {
      await transferBranch.mutateAsync({
        newBranchId: values.newBranchId,
        reason: values.reason?.trim() ? values.reason.trim() : null,
      })
      onClose()
    } catch (error) {
      setServerError(getErrorMessage(error, 'Could not transfer this student.'))
    }
  }

  return (
    <Modal title={`Transfer ${student.fullName} to another branch`} onClose={onClose}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <Select label="New branch" {...register('newBranchId')} error={errors.newBranchId?.message}>
          <option value="">Select a branch</option>
          {otherBranches.map((branch) => (
            <option key={branch.id} value={branch.id}>
              {branch.name}
            </option>
          ))}
        </Select>

        <Input label="Reason (optional)" {...register('reason')} error={errors.reason?.message} />

        {serverError && <p className="text-sm text-coral">{serverError}</p>}

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? 'Transferring...' : 'Transfer'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}

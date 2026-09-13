import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { getErrorMessage } from '@/shared/api/errors'
import { Button } from '@/shared/ui/Button'
import { Input } from '@/shared/ui/Input'
import { Modal } from '@/shared/ui/Modal'
import { useGenerateStaffPayrollRun } from './api'
import type { GenerateStaffPayrollRunInput } from './types'

export function GenerateStaffRunModal({ userId, onClose }: { userId: string; onClose: () => void }) {
  const generateRun = useGenerateStaffPayrollRun(userId)
  const [serverError, setServerError] = useState<string | null>(null)
  const { register, handleSubmit } = useForm<GenerateStaffPayrollRunInput>()

  async function onSubmit(values: GenerateStaffPayrollRunInput) {
    setServerError(null)
    try {
      await generateRun.mutateAsync({ ...values, amount: Number(values.amount) })
      onClose()
    } catch (error) {
      setServerError(getErrorMessage(error, 'Could not generate this payroll run.'))
    }
  }

  return (
    <Modal title="Generate payroll run" onClose={onClose}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <Input label="Period start" type="date" {...register('periodStart', { required: true })} />
        <Input label="Period end" type="date" {...register('periodEnd', { required: true })} />
        <Input
          label="Amount"
          type="number"
          step="0.01"
          {...register('amount', { required: true, valueAsNumber: true })}
        />

        {serverError && <p className="text-sm text-coral">{serverError}</p>}

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={generateRun.isPending}>
            {generateRun.isPending ? 'Generating...' : 'Generate'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}

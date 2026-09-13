import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { getErrorMessage } from '@/shared/api/errors'
import { Button } from '@/shared/ui/Button'
import { Input } from '@/shared/ui/Input'
import { Modal } from '@/shared/ui/Modal'
import { useGeneratePayrollRun } from './api'
import type { GeneratePayrollRunInput } from './types'

export function GenerateRunModal({ teacherId, onClose }: { teacherId: string; onClose: () => void }) {
  const generateRun = useGeneratePayrollRun(teacherId)
  const [serverError, setServerError] = useState<string | null>(null)
  const { register, handleSubmit } = useForm<GeneratePayrollRunInput>()

  async function onSubmit(values: GeneratePayrollRunInput) {
    setServerError(null)
    try {
      await generateRun.mutateAsync(values)
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

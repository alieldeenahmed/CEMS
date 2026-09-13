import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useBranches } from '@/features/branches/api'
import { useCourses } from '@/features/courses/api'
import { getErrorMessage } from '@/shared/api/errors'
import { Button } from '@/shared/ui/Button'
import { Input, Select } from '@/shared/ui/Input'
import { Modal } from '@/shared/ui/Modal'
import { useCreateInvoice, usePackagesForCourse } from './api'

interface InvoiceFormValues {
  mode: 'package' | 'adhoc'
  courseId: string
  packageId: string
  amount: number
  dueDate: string
}

export function InvoiceFormModal({ studentId, onClose }: { studentId: string; onClose: () => void }) {
  const { data: courses } = useCourses()
  const { data: branches } = useBranches()
  const createInvoice = useCreateInvoice(studentId)
  const [serverError, setServerError] = useState<string | null>(null)

  const { register, handleSubmit, watch } = useForm<InvoiceFormValues>({
    defaultValues: { mode: 'package', dueDate: '' },
  })

  const mode = watch('mode')
  const courseId = watch('courseId')
  const { data: packages } = usePackagesForCourse(mode === 'package' ? courseId || null : null)
  const branchNameById = new Map(branches?.map((b) => [b.id, b.name]))

  async function onSubmit(values: InvoiceFormValues) {
    setServerError(null)
    try {
      await createInvoice.mutateAsync({
        packageId: values.mode === 'package' ? values.packageId || null : null,
        amount: values.mode === 'adhoc' ? Number(values.amount) : null,
        dueDate: values.dueDate,
      })
      onClose()
    } catch (error) {
      setServerError(getErrorMessage(error, 'Could not create this invoice.'))
    }
  }

  return (
    <Modal title="New invoice" onClose={onClose}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div className="flex gap-4 text-sm">
          <label className="flex items-center gap-1.5">
            <input type="radio" value="package" {...register('mode')} /> From a package
          </label>
          <label className="flex items-center gap-1.5">
            <input type="radio" value="adhoc" {...register('mode')} /> Ad-hoc amount
          </label>
        </div>

        {mode === 'package' ? (
          <>
            <Select label="Course" {...register('courseId')}>
              <option value="">Select a course</option>
              {courses?.map((course) => (
                <option key={course.id} value={course.id}>
                  {course.name} · {branchNameById.get(course.branchId) ?? 'Unknown branch'}
                </option>
              ))}
            </Select>

            {courseId && (
              <Select label="Package" {...register('packageId')}>
                <option value="">Select a package</option>
                {packages?.map((pkg) => (
                  <option key={pkg.id} value={pkg.id}>
                    {pkg.sessionCount} sessions · ${pkg.price}
                  </option>
                ))}
              </Select>
            )}
          </>
        ) : (
          <Input
            label="Amount"
            type="number"
            step="0.01"
            {...register('amount', { valueAsNumber: true })}
          />
        )}

        <Input label="Due date" type="date" {...register('dueDate', { required: true })} />

        {serverError && <p className="text-sm text-coral">{serverError}</p>}

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={createInvoice.isPending}>
            {createInvoice.isPending ? 'Creating...' : 'Create invoice'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}

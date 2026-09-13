import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Button } from '@/shared/ui/Button'
import { Input, Select } from '@/shared/ui/Input'
import { Modal } from '@/shared/ui/Modal'
import { useCreateTeacherProfile, useTeacherCandidates, useUpdateTeacher } from './api'
import type { Teacher } from './types'

const teacherSchema = z.object({
  userId: z.string().optional(),
  hireDate: z.string().min(1, 'Hire date is required'),
  payType: z.enum(['Hourly', 'PerSession']),
  payRate: z.number().positive('Pay rate must be greater than 0'),
})

type TeacherFormValues = z.infer<typeof teacherSchema>

interface TeacherFormModalProps {
  teacher: Teacher | null
  onClose: () => void
}

export function TeacherFormModal({ teacher, onClose }: TeacherFormModalProps) {
  const { data: candidates } = useTeacherCandidates()
  const createTeacherProfile = useCreateTeacherProfile()
  const updateTeacher = useUpdateTeacher()
  const [serverError, setServerError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<TeacherFormValues>({
    resolver: zodResolver(teacherSchema),
    defaultValues: {
      hireDate: teacher?.hireDate ?? '',
      payType: teacher?.payType ?? 'Hourly',
      payRate: teacher?.payRate ?? 0,
    },
  })

  async function onSubmit(values: TeacherFormValues) {
    setServerError(null)
    try {
      if (teacher) {
        await updateTeacher.mutateAsync({
          id: teacher.id,
          hireDate: values.hireDate,
          payType: values.payType,
          payRate: values.payRate,
        })
      } else {
        if (!values.userId) {
          setServerError('Select a staff account.')
          return
        }
        await createTeacherProfile.mutateAsync({
          userId: values.userId,
          hireDate: values.hireDate,
          payType: values.payType,
          payRate: values.payRate,
        })
      }
      onClose()
    } catch {
      setServerError('Could not save the teacher profile.')
    }
  }

  return (
    <Modal title={teacher ? 'Edit teacher profile' : 'New teacher profile'} onClose={onClose}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        {!teacher && (
          <Select label="Staff account" {...register('userId')}>
            <option value="">Select a Teacher-role staff account</option>
            {candidates?.map((user) => (
              <option key={user.userId} value={user.userId}>
                {user.fullName} ({user.email})
              </option>
            ))}
          </Select>
        )}

        <Input
          label="Hire date"
          type="date"
          {...register('hireDate')}
          error={errors.hireDate?.message}
        />

        <Select label="Pay type" {...register('payType')}>
          <option value="Hourly">Hourly</option>
          <option value="PerSession">Per Session</option>
        </Select>

        <Input
          label="Pay rate"
          type="number"
          step="0.01"
          {...register('payRate', { valueAsNumber: true })}
          error={errors.payRate?.message}
        />

        {serverError && <p className="text-sm text-coral">{serverError}</p>}

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? 'Saving...' : 'Save'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}

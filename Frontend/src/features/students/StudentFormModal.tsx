import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { useBranches } from '@/features/branches/api'
import { Button } from '@/shared/ui/Button'
import { Input, Select } from '@/shared/ui/Input'
import { Modal } from '@/shared/ui/Modal'
import { useCreateStudent, useUpdateStudent } from './api'
import type { Student } from './types'

const studentSchema = z.object({
  fullName: z.string().min(1, 'Name is required'),
  dateOfBirth: z.string().min(1, 'Date of birth is required'),
  gender: z.enum(['Male', 'Female']),
  status: z.enum(['Active', 'Paused', 'Graduated']),
  branchId: z.string().min(1, 'Branch is required'),
})

type StudentFormValues = z.infer<typeof studentSchema>

interface StudentFormModalProps {
  student: Student | null
  onClose: () => void
}

export function StudentFormModal({ student, onClose }: StudentFormModalProps) {
  const { data: branches } = useBranches()
  const createStudent = useCreateStudent()
  const updateStudent = useUpdateStudent()

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<StudentFormValues>({
    resolver: zodResolver(studentSchema),
    defaultValues: {
      fullName: student?.fullName ?? '',
      dateOfBirth: student?.dateOfBirth ?? '',
      gender: student?.gender ?? 'Male',
      status: student?.status ?? 'Active',
      branchId: student?.currentBranchId ?? '',
    },
  })

  const isSaving = createStudent.isPending || updateStudent.isPending

  async function onSubmit(values: StudentFormValues) {
    if (student) {
      await updateStudent.mutateAsync({
        id: student.id,
        fullName: values.fullName,
        dateOfBirth: values.dateOfBirth,
        gender: values.gender,
        status: values.status,
      })
    } else {
      await createStudent.mutateAsync({
        fullName: values.fullName,
        dateOfBirth: values.dateOfBirth,
        gender: values.gender,
        branchId: values.branchId,
      })
    }
    onClose()
  }

  return (
    <Modal title={student ? 'Edit student' : 'New student'} onClose={onClose}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <Input label="Full name" {...register('fullName')} error={errors.fullName?.message} />
        <Input
          label="Date of birth"
          type="date"
          {...register('dateOfBirth')}
          error={errors.dateOfBirth?.message}
        />

        <Select label="Gender" {...register('gender')}>
          <option value="Male">Male</option>
          <option value="Female">Female</option>
        </Select>

        {student ? (
          <Select label="Status" {...register('status')}>
            <option value="Active">Active</option>
            <option value="Paused">Paused</option>
            <option value="Graduated">Graduated</option>
          </Select>
        ) : (
          <Select label="Branch" {...register('branchId')} error={errors.branchId?.message}>
            <option value="">Select a branch</option>
            {branches?.map((branch) => (
              <option key={branch.id} value={branch.id}>
                {branch.name}
              </option>
            ))}
          </Select>
        )}

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

import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { useBranches } from '@/features/branches/api'
import { getErrorMessage } from '@/shared/api/errors'
import { Button } from '@/shared/ui/Button'
import { Input, Select } from '@/shared/ui/Input'
import { Modal } from '@/shared/ui/Modal'
import { useAllGuardians, useCreateStudent, useUpdateStudent } from './api'
import type { Student } from './types'

const editSchema = z.object({
  fullName: z.string().min(1, 'Name is required'),
  dateOfBirth: z.string().min(1, 'Date of birth is required'),
  gender: z.enum(['Male', 'Female']),
  status: z.enum(['Active', 'Paused', 'Graduated']),
})

// A student must always have a guardian attached at creation - no "add it later" path exists, so
// the guardian fields here are just as required as the student's own name or date of birth.
export const createSchema = z
  .object({
    fullName: z.string().min(1, 'Name is required'),
    dateOfBirth: z.string().min(1, 'Date of birth is required'),
    gender: z.enum(['Male', 'Female']),
    branchId: z.string().min(1, 'Branch is required'),
    guardianMode: z.enum(['existing', 'new']),
    existingGuardianId: z.string().optional(),
    newGuardianFullName: z.string().optional(),
    newGuardianPhone: z.string().optional(),
    newGuardianEmail: z.string().optional(),
    relationshipType: z.enum(['Mother', 'Father', 'Guardian']),
    isPrimaryContact: z.boolean(),
  })
  .refine((data) => data.guardianMode !== 'existing' || !!data.existingGuardianId, {
    message: 'Select a guardian',
    path: ['existingGuardianId'],
  })
  .refine((data) => data.guardianMode !== 'new' || !!data.newGuardianFullName, {
    message: 'Guardian name is required',
    path: ['newGuardianFullName'],
  })
  .refine((data) => data.guardianMode !== 'new' || !!data.newGuardianPhone, {
    message: 'Guardian phone is required',
    path: ['newGuardianPhone'],
  })
  .refine((data) => data.guardianMode !== 'new' || !!data.newGuardianEmail, {
    message: 'Guardian email is required',
    path: ['newGuardianEmail'],
  })

type EditFormValues = z.infer<typeof editSchema>
type CreateFormValues = z.infer<typeof createSchema>

interface StudentFormModalProps {
  student: Student | null
  onClose: () => void
}

export function StudentFormModal({ student, onClose }: StudentFormModalProps) {
  return student ? (
    <EditStudentModal student={student} onClose={onClose} />
  ) : (
    <CreateStudentModal onClose={onClose} />
  )
}

function EditStudentModal({ student, onClose }: { student: Student; onClose: () => void }) {
  const updateStudent = useUpdateStudent()

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<EditFormValues>({
    resolver: zodResolver(editSchema),
    defaultValues: {
      fullName: student.fullName,
      dateOfBirth: student.dateOfBirth,
      gender: student.gender,
      status: student.status,
    },
  })

  async function onSubmit(values: EditFormValues) {
    await updateStudent.mutateAsync({ id: student.id, ...values })
    onClose()
  }

  return (
    <Modal title="Edit student" onClose={onClose}>
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

        <Select label="Status" {...register('status')}>
          <option value="Active">Active</option>
          <option value="Paused">Paused</option>
          <option value="Graduated">Graduated</option>
        </Select>

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={updateStudent.isPending}>
            {updateStudent.isPending ? 'Saving...' : 'Save'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}

function CreateStudentModal({ onClose }: { onClose: () => void }) {
  const { data: branches } = useBranches()
  const { data: guardians } = useAllGuardians()
  const createStudent = useCreateStudent()

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors },
  } = useForm<CreateFormValues>({
    resolver: zodResolver(createSchema),
    defaultValues: { guardianMode: 'existing', relationshipType: 'Guardian', isPrimaryContact: true },
  })

  const guardianMode = watch('guardianMode')

  async function onSubmit(values: CreateFormValues) {
    try {
      await createStudent.mutateAsync({
        fullName: values.fullName,
        dateOfBirth: values.dateOfBirth,
        gender: values.gender,
        branchId: values.branchId,
        existingGuardianId: values.guardianMode === 'existing' ? (values.existingGuardianId ?? null) : null,
        newGuardianFullName: values.guardianMode === 'new' ? (values.newGuardianFullName ?? null) : null,
        newGuardianPhone: values.guardianMode === 'new' ? (values.newGuardianPhone ?? null) : null,
        newGuardianEmail: values.guardianMode === 'new' ? (values.newGuardianEmail ?? null) : null,
        relationshipType: values.relationshipType,
        isPrimaryContact: values.isPrimaryContact,
      })
      onClose()
    } catch (error) {
      window.alert(getErrorMessage(error, 'Could not create this student.'))
    }
  }

  return (
    <Modal title="New student" onClose={onClose} bodyClassName="h-[520px]">
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

        <Select label="Branch" {...register('branchId')} error={errors.branchId?.message}>
          <option value="">Select a branch</option>
          {branches?.map((branch) => (
            <option key={branch.id} value={branch.id}>
              {branch.name}
            </option>
          ))}
        </Select>

        <div className="border-t border-line pt-4">
          <p className="mb-2 text-sm font-medium text-ink">Guardian</p>
          <p className="mb-3 text-xs text-muted">Every student must have at least one guardian on file.</p>

          <div className="mb-3 flex gap-4 text-sm">
            <label className="flex items-center gap-1.5">
              <input type="radio" value="existing" {...register('guardianMode')} /> Existing guardian
            </label>
            <label className="flex items-center gap-1.5">
              <input type="radio" value="new" {...register('guardianMode')} /> New guardian
            </label>
          </div>

          {guardianMode === 'existing' ? (
            <Select {...register('existingGuardianId')} error={errors.existingGuardianId?.message}>
              <option value="">Select a guardian</option>
              {guardians?.map((guardian) => (
                <option key={guardian.id} value={guardian.id}>
                  {guardian.fullName} ({guardian.email})
                </option>
              ))}
            </Select>
          ) : (
            <div className="space-y-3">
              <Input
                placeholder="Full name"
                {...register('newGuardianFullName')}
                error={errors.newGuardianFullName?.message}
              />
              <Input
                placeholder="Phone"
                {...register('newGuardianPhone')}
                error={errors.newGuardianPhone?.message}
              />
              <Input
                placeholder="Email"
                type="email"
                {...register('newGuardianEmail')}
                error={errors.newGuardianEmail?.message}
              />
            </div>
          )}

          <div className="mt-3 flex items-center gap-4">
            <Select {...register('relationshipType')} className="w-40">
              <option value="Mother">Mother</option>
              <option value="Father">Father</option>
              <option value="Guardian">Guardian</option>
            </Select>
            <label className="flex items-center gap-1.5 text-sm text-ink">
              <input type="checkbox" {...register('isPrimaryContact')} /> Primary contact
            </label>
          </div>
        </div>

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={createStudent.isPending}>
            {createStudent.isPending ? 'Creating...' : 'Create'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}

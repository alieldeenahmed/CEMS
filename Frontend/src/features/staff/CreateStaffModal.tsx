import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { useBranches } from '@/features/branches/api'
import { Button } from '@/shared/ui/Button'
import { Input, Select } from '@/shared/ui/Input'
import { Modal } from '@/shared/ui/Modal'
import { useCreateStaffUser } from './api'

const STAFF_ROLES = ['BranchManager', 'FrontDesk', 'Teacher'] as const
const BRANCH_SCOPED_ROLES: string[] = ['BranchManager', 'FrontDesk']

const staffSchema = z
  .object({
    email: z.string().min(1, 'Email is required').email('Enter a valid email address'),
    password: z.string().min(8, 'Password must be at least 8 characters'),
    fullName: z.string().min(1, 'Full name is required'),
    phoneNumber: z.string().min(1, 'Phone number is required'),
    role: z.enum(STAFF_ROLES),
    branchId: z.string().optional(),
  })
  .refine((data) => !BRANCH_SCOPED_ROLES.includes(data.role) || !!data.branchId, {
    message: 'Branch is required for this role',
    path: ['branchId'],
  })

type StaffFormValues = z.infer<typeof staffSchema>

export function CreateStaffModal({ onClose }: { onClose: () => void }) {
  const { data: branches } = useBranches()
  const createStaffUser = useCreateStaffUser()
  const [serverError, setServerError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<StaffFormValues>({
    resolver: zodResolver(staffSchema),
    defaultValues: { role: 'BranchManager' },
  })

  const role = watch('role')
  const isBranchScoped = BRANCH_SCOPED_ROLES.includes(role)

  async function onSubmit(values: StaffFormValues) {
    setServerError(null)
    try {
      await createStaffUser.mutateAsync({
        email: values.email,
        password: values.password,
        fullName: values.fullName,
        phoneNumber: values.phoneNumber,
        role: values.role,
        branchId: isBranchScoped ? (values.branchId ?? null) : null,
      })
      onClose()
    } catch {
      setServerError('Could not create the account. The email may already be in use.')
    }
  }

  return (
    <Modal title="New staff account" onClose={onClose}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <Input label="Full name" {...register('fullName')} error={errors.fullName?.message} />
        <Input label="Email" type="email" {...register('email')} error={errors.email?.message} />
        <Input
          label="Password"
          type="password"
          {...register('password')}
          error={errors.password?.message}
        />
        <Input
          label="Phone number"
          {...register('phoneNumber')}
          error={errors.phoneNumber?.message}
        />

        <Select label="Role" {...register('role')}>
          <option value="BranchManager">Branch Manager</option>
          <option value="FrontDesk">Front Desk</option>
          <option value="Teacher">Teacher</option>
        </Select>

        {isBranchScoped && (
          <Select label="Branch" {...register('branchId')} error={errors.branchId?.message}>
            <option value="">Select a branch</option>
            {branches?.map((branch) => (
              <option key={branch.id} value={branch.id}>
                {branch.name}
              </option>
            ))}
          </Select>
        )}

        {serverError && <p className="text-sm text-coral">{serverError}</p>}

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? 'Creating...' : 'Create account'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}

import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Button } from '@/shared/ui/Button'
import { Input } from '@/shared/ui/Input'
import { Modal } from '@/shared/ui/Modal'
import { useResetStaffPassword } from './api'
import type { StaffUser } from './types'

const resetPasswordSchema = z.object({
  newPassword: z.string().min(8, 'Password must be at least 8 characters'),
})

type ResetPasswordFormValues = z.infer<typeof resetPasswordSchema>

export function ResetPasswordModal({ user, onClose }: { user: StaffUser; onClose: () => void }) {
  const resetPassword = useResetStaffPassword()
  const [serverError, setServerError] = useState<string | null>(null)
  const [succeeded, setSucceeded] = useState(false)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ResetPasswordFormValues>({ resolver: zodResolver(resetPasswordSchema) })

  async function onSubmit(values: ResetPasswordFormValues) {
    setServerError(null)
    try {
      await resetPassword.mutateAsync({ userId: user.userId, newPassword: values.newPassword })
      setSucceeded(true)
    } catch {
      setServerError('Could not reset this password. You may not have access to reset this account.')
    }
  }

  return (
    <Modal title={`Reset password for ${user.fullName}`} onClose={onClose}>
      {succeeded ? (
        <div className="space-y-4">
          <p className="text-sm text-ink">
            Password reset. Share the new password with {user.fullName} directly -- it isn't emailed or
            shown anywhere else.
          </p>
          <div className="flex justify-end">
            <Button onClick={onClose}>Done</Button>
          </div>
        </div>
      ) : (
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <p className="text-xs text-muted">
            Sets this account's password directly, no email involved -- you'll need to relay it to{' '}
            {user.fullName} yourself.
          </p>

          <Input
            label="New password"
            type="password"
            {...register('newPassword')}
            error={errors.newPassword?.message}
          />

          {serverError && <p className="text-sm text-coral">{serverError}</p>}

          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="secondary" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Resetting...' : 'Reset password'}
            </Button>
          </div>
        </form>
      )}
    </Modal>
  )
}

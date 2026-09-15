import { isAxiosError } from 'axios'
import { useState } from 'react'
import { useAuth } from '@/features/auth/AuthContext'
import { ROLES } from '@/features/auth/constants'
import { useTeachers } from '@/features/teachers/api'
import { Button } from '@/shared/ui/Button'
import { Select } from '@/shared/ui/Input'
import { Modal } from '@/shared/ui/Modal'
import { useSubstituteSessionTeacher } from './api'
import { formatUtcForDisplay } from './time'
import type { CourseSession } from './types'

export function SubstituteTeacherModal({
  session,
  courseId,
  branchId,
  onClose,
}: {
  session: CourseSession
  courseId: string
  branchId: string
  onClose: () => void
}) {
  const { hasRole } = useAuth()
  const canOverride = hasRole(ROLES.Owner, ROLES.BranchManager)

  const { data: teachers } = useTeachers()
  const substituteTeacher = useSubstituteSessionTeacher(courseId)

  const [newTeacherId, setNewTeacherId] = useState('')
  const [conflicts, setConflicts] = useState<string[] | null>(null)
  const [overrideReason, setOverrideReason] = useState('')
  const [genericError, setGenericError] = useState<string | null>(null)

  const branchTeachers = teachers?.filter((t) => t.branchIds.includes(branchId) && t.id !== session.teacherId) ?? []

  async function submit(override: boolean) {
    setConflicts(null)
    setGenericError(null)
    try {
      await substituteTeacher.mutateAsync({
        id: session.id,
        newTeacherId,
        override,
        overrideReason: override ? overrideReason : null,
      })
      onClose()
    } catch (error) {
      if (isAxiosError(error) && error.response?.status === 409) {
        setConflicts(error.response.data?.conflicts ?? ['A scheduling conflict was detected.'])
      } else if (isAxiosError(error) && error.response) {
        const errors = error.response.data?.errors
        setGenericError(Array.isArray(errors) ? errors.join(' ') : 'Could not substitute the teacher.')
      } else {
        setGenericError('Could not substitute the teacher.')
      }
    }
  }

  return (
    <Modal title="Substitute teacher" onClose={onClose}>
      <div className="space-y-4">
        <p className="text-xs text-muted">
          {formatUtcForDisplay(session.startUtc)} – {formatUtcForDisplay(session.endUtc)}. The room and
          time stay the same -- only the teacher changes.
        </p>

        <Select label="Substitute teacher" value={newTeacherId} onChange={(e) => setNewTeacherId(e.target.value)}>
          <option value="">Select a teacher</option>
          {branchTeachers.map((teacher) => (
            <option key={teacher.id} value={teacher.id}>
              {teacher.fullName}
            </option>
          ))}
        </Select>

        {genericError && <p className="text-sm text-coral">{genericError}</p>}

        {conflicts && (
          <div className="rounded-md border border-coral/40 bg-coral/5 p-3">
            <p className="mb-1.5 text-sm font-medium text-coral">Scheduling conflict</p>
            <ul className="mb-2 list-inside list-disc text-sm text-coral">
              {conflicts.map((conflict) => (
                <li key={conflict}>{conflict}</li>
              ))}
            </ul>

            {canOverride ? (
              <div className="space-y-2">
                <input
                  placeholder="Reason for overriding this conflict"
                  value={overrideReason}
                  onChange={(e) => setOverrideReason(e.target.value)}
                  className="w-full rounded-md border border-line bg-paper px-3 py-2 text-sm text-ink outline-none focus:border-navy"
                />
                <Button
                  type="button"
                  variant="danger"
                  disabled={!overrideReason.trim() || substituteTeacher.isPending}
                  onClick={() => submit(true)}
                >
                  Substitute anyway
                </Button>
              </div>
            ) : (
              <p className="text-xs text-muted">
                Only an Owner or Branch Manager can override a scheduling conflict.
              </p>
            )}
          </div>
        )}

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            type="button"
            disabled={!newTeacherId || substituteTeacher.isPending}
            onClick={() => submit(false)}
          >
            {substituteTeacher.isPending ? 'Substituting...' : 'Substitute'}
          </Button>
        </div>
      </div>
    </Modal>
  )
}

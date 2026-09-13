import { isAxiosError } from 'axios'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useRoomsByBranch } from '@/features/branches/api'
import { useAuth } from '@/features/auth/AuthContext'
import { ROLES } from '@/features/auth/constants'
import type { Course } from '@/features/courses/types'
import { useTeachers } from '@/features/teachers/api'
import { Button } from '@/shared/ui/Button'
import { Select } from '@/shared/ui/Input'
import { Modal } from '@/shared/ui/Modal'
import { useCreateSession } from './api'
import { toUtcIso } from './time'

interface SessionFormValues {
  roomId: string
  teacherId: string
  startLocal: string
  endLocal: string
}

export function CreateSessionModal({ course, onClose }: { course: Course; onClose: () => void }) {
  const { hasRole } = useAuth()
  const canOverride = hasRole(ROLES.Owner, ROLES.BranchManager)

  const { data: rooms } = useRoomsByBranch(course.branchId)
  const { data: teachers } = useTeachers()
  const createSession = useCreateSession(course.id)

  const [conflicts, setConflicts] = useState<string[] | null>(null)
  const [overrideReason, setOverrideReason] = useState('')
  const [genericError, setGenericError] = useState<string | null>(null)

  const branchTeachers = teachers?.filter((t) => t.branchIds.includes(course.branchId)) ?? []

  const { register, handleSubmit, getValues } = useForm<SessionFormValues>()

  async function submit(values: SessionFormValues, override: boolean) {
    setConflicts(null)
    setGenericError(null)
    try {
      await createSession.mutateAsync({
        roomId: values.roomId,
        teacherId: values.teacherId,
        startUtc: toUtcIso(values.startLocal),
        endUtc: toUtcIso(values.endLocal),
        override,
        overrideReason: override ? overrideReason : null,
      })
      onClose()
    } catch (error) {
      if (isAxiosError(error) && error.response?.status === 409) {
        setConflicts(error.response.data?.conflicts ?? ['A scheduling conflict was detected.'])
      } else if (isAxiosError(error) && error.response) {
        const errors = error.response.data?.errors
        setGenericError(Array.isArray(errors) ? errors.join(' ') : 'Could not schedule this session.')
      } else {
        setGenericError('Could not schedule this session.')
      }
    }
  }

  function onSubmit(values: SessionFormValues) {
    return submit(values, false)
  }

  function onOverride() {
    return submit(getValues(), true)
  }

  return (
    <Modal title="New session" onClose={onClose}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <Select label="Room" {...register('roomId', { required: true })}>
          <option value="">Select a room</option>
          {rooms?.map((room) => (
            <option key={room.id} value={room.id}>
              {room.name} (capacity {room.capacity})
            </option>
          ))}
        </Select>

        <Select label="Teacher" {...register('teacherId', { required: true })}>
          <option value="">Select a teacher</option>
          {branchTeachers.map((teacher) => (
            <option key={teacher.id} value={teacher.id}>
              {teacher.fullName}
            </option>
          ))}
        </Select>

        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="mb-1 block text-sm font-medium text-ink">Start</label>
            <input
              type="datetime-local"
              className="w-full rounded-md border border-line bg-paper px-3 py-2 text-sm text-ink outline-none focus:border-navy"
              {...register('startLocal', { required: true })}
            />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-ink">End</label>
            <input
              type="datetime-local"
              className="w-full rounded-md border border-line bg-paper px-3 py-2 text-sm text-ink outline-none focus:border-navy"
              {...register('endLocal', { required: true })}
            />
          </div>
        </div>

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
                  disabled={!overrideReason.trim() || createSession.isPending}
                  onClick={onOverride}
                >
                  Schedule anyway
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
          <Button type="submit" disabled={createSession.isPending}>
            {createSession.isPending ? 'Scheduling...' : 'Schedule session'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}

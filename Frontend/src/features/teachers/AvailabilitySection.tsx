import { Plus, Trash2 } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { useBranches } from '@/features/branches/api'
import { Button } from '@/shared/ui/Button'
import { useAddAvailability, useAvailabilityForTeacher, useRemoveAvailability } from './api'
import { DAYS_OF_WEEK, type AddAvailabilityInput } from './types'

export function AvailabilitySection({ teacherId }: { teacherId: string }) {
  const { data: availability, isLoading } = useAvailabilityForTeacher(teacherId)
  const { data: branches } = useBranches()
  const addAvailability = useAddAvailability(teacherId)
  const removeAvailability = useRemoveAvailability(teacherId)

  const branchNameById = new Map(branches?.map((b) => [b.id, b.name]))

  const { register, handleSubmit, reset } = useForm<AddAvailabilityInput>({
    defaultValues: { dayOfWeek: 'Monday', startTime: '09:00', endTime: '17:00' },
  })

  function toTimeOnlyString(value: string) {
    return value.length === 5 ? `${value}:00` : value
  }

  async function onSubmit(values: AddAvailabilityInput) {
    await addAvailability.mutateAsync({
      ...values,
      startTime: toTimeOnlyString(values.startTime),
      endTime: toTimeOnlyString(values.endTime),
    })
    reset({ dayOfWeek: 'Monday', startTime: '09:00', endTime: '17:00', branchId: values.branchId })
  }

  if (isLoading) {
    return <p className="text-sm text-muted">Loading availability...</p>
  }

  return (
    <div>
      <p className="mb-2 text-xs font-medium uppercase tracking-wide text-muted">Availability</p>

      {availability && availability.length > 0 ? (
        <ul className="mb-3 space-y-1.5">
          {availability.map((slot) => (
            <li
              key={slot.id}
              className="flex items-center justify-between rounded-md bg-paper px-3 py-2 text-sm"
            >
              <span className="text-ink">
                {slot.dayOfWeek} · {slot.startTime.slice(0, 5)}–{slot.endTime.slice(0, 5)} ·{' '}
                <span className="text-muted">{branchNameById.get(slot.branchId) ?? 'Unknown'}</span>
              </span>
              <button
                type="button"
                aria-label="Remove availability"
                onClick={() => removeAvailability.mutate(slot.id)}
                className="text-muted transition-colors hover:text-coral"
              >
                <Trash2 size={15} />
              </button>
            </li>
          ))}
        </ul>
      ) : (
        <p className="mb-3 text-sm text-muted">No availability set.</p>
      )}

      <form onSubmit={handleSubmit(onSubmit)} className="flex flex-wrap items-end gap-2">
        <select
          {...register('branchId', { required: true })}
          className="rounded-md border border-line bg-paper px-2 py-1.5 text-sm outline-none focus:border-navy"
        >
          <option value="">Branch</option>
          {branches?.map((branch) => (
            <option key={branch.id} value={branch.id}>
              {branch.name}
            </option>
          ))}
        </select>

        <select
          {...register('dayOfWeek')}
          className="rounded-md border border-line bg-paper px-2 py-1.5 text-sm outline-none focus:border-navy"
        >
          {DAYS_OF_WEEK.map((day) => (
            <option key={day} value={day}>
              {day}
            </option>
          ))}
        </select>

        <input
          type="time"
          {...register('startTime', { required: true })}
          className="rounded-md border border-line bg-paper px-2 py-1.5 text-sm outline-none focus:border-navy"
        />
        <input
          type="time"
          {...register('endTime', { required: true })}
          className="rounded-md border border-line bg-paper px-2 py-1.5 text-sm outline-none focus:border-navy"
        />

        <Button type="submit" variant="secondary" disabled={addAvailability.isPending}>
          <Plus size={15} />
          Add
        </Button>
      </form>
    </div>
  )
}

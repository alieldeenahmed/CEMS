import { Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { getErrorMessage } from '@/shared/api/errors'
import { Button } from '@/shared/ui/Button'
import { Input } from '@/shared/ui/Input'
import { useCreateRoom, useDeleteRoom, useRoomsByBranch, useUpdateRoom } from './api'
import type { RoomInput } from './types'

export function RoomsSection({ branchId }: { branchId: string }) {
  const { data: rooms, isLoading } = useRoomsByBranch(branchId)
  const createRoom = useCreateRoom(branchId)
  const updateRoom = useUpdateRoom(branchId)
  const deleteRoom = useDeleteRoom(branchId)
  const [editingRoomId, setEditingRoomId] = useState<string | null>(null)

  const { register, handleSubmit, reset } = useForm<RoomInput>({
    defaultValues: { name: '', capacity: 1 },
  })

  async function onAddRoom(values: RoomInput) {
    await createRoom.mutateAsync({ name: values.name, capacity: Number(values.capacity) })
    reset()
  }

  if (isLoading) {
    return <p className="text-sm text-muted">Loading rooms...</p>
  }

  return (
    <div className="border-t border-line bg-parchment/50 p-4">
      <p className="mb-3 text-xs font-medium uppercase tracking-wide text-muted">Rooms</p>

      {rooms && rooms.length > 0 ? (
        <ul className="mb-4 space-y-1.5">
          {rooms.map((room) => (
            <li
              key={room.id}
              className="flex items-center justify-between rounded-md bg-paper px-3 py-2 text-sm"
            >
              {editingRoomId === room.id ? (
                <RoomEditRow
                  initialName={room.name}
                  initialCapacity={room.capacity}
                  onCancel={() => setEditingRoomId(null)}
                  onSave={async (values) => {
                    await updateRoom.mutateAsync({ id: room.id, ...values })
                    setEditingRoomId(null)
                  }}
                />
              ) : (
                <>
                  <button
                    type="button"
                    className="flex-1 text-left text-ink"
                    onClick={() => setEditingRoomId(room.id)}
                  >
                    {room.name} <span className="text-muted">· capacity {room.capacity}</span>
                  </button>
                  <button
                    type="button"
                    aria-label={`Delete ${room.name}`}
                    onClick={() =>
                      deleteRoom.mutate(room.id, {
                        onError: (error) => window.alert(getErrorMessage(error, 'Could not delete this room.')),
                      })
                    }
                    className="text-muted transition-colors hover:text-coral"
                  >
                    <Trash2 size={15} />
                  </button>
                </>
              )}
            </li>
          ))}
        </ul>
      ) : (
        <p className="mb-4 text-sm text-muted">No rooms yet.</p>
      )}

      <form onSubmit={handleSubmit(onAddRoom)} className="flex items-end gap-2">
        <div className="flex-1">
          <Input placeholder="Room name" {...register('name', { required: true })} />
        </div>
        <div className="w-28">
          <Input
            type="number"
            min={1}
            placeholder="Capacity"
            {...register('capacity', { required: true, valueAsNumber: true })}
          />
        </div>
        <Button type="submit" variant="secondary" disabled={createRoom.isPending}>
          <Plus size={15} />
          Add
        </Button>
      </form>
    </div>
  )
}

function RoomEditRow({
  initialName,
  initialCapacity,
  onSave,
  onCancel,
}: {
  initialName: string
  initialCapacity: number
  onSave: (values: RoomInput) => void
  onCancel: () => void
}) {
  const { register, handleSubmit } = useForm<RoomInput>({
    defaultValues: { name: initialName, capacity: initialCapacity },
  })

  return (
    <form onSubmit={handleSubmit(onSave)} className="flex w-full items-center gap-2">
      <input
        className="flex-1 rounded-md border border-line px-2 py-1 text-sm outline-none focus:border-navy"
        {...register('name', { required: true })}
      />
      <input
        type="number"
        min={1}
        className="w-20 rounded-md border border-line px-2 py-1 text-sm outline-none focus:border-navy"
        {...register('capacity', { required: true, valueAsNumber: true })}
      />
      <Button type="submit" variant="secondary" className="px-2 py-1 text-xs">
        Save
      </Button>
      <Button type="button" variant="ghost" className="px-2 py-1 text-xs" onClick={onCancel}>
        Cancel
      </Button>
    </form>
  )
}

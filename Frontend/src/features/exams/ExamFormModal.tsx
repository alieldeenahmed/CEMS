import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Button } from '@/shared/ui/Button'
import { Input } from '@/shared/ui/Input'
import { Modal } from '@/shared/ui/Modal'
import { useCreateExam, useUpdateExam } from './api'
import type { Exam } from './types'

const examSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  maxScore: z.number().positive('Max score must be greater than 0'),
  examDate: z.string().min(1, 'Exam date is required'),
})

type ExamFormValues = z.infer<typeof examSchema>

export function ExamFormModal({
  courseId,
  exam,
  onClose,
}: {
  courseId: string
  exam: Exam | null
  onClose: () => void
}) {
  const createExam = useCreateExam(courseId)
  const updateExam = useUpdateExam(courseId)

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ExamFormValues>({
    resolver: zodResolver(examSchema),
    defaultValues: {
      name: exam?.name ?? '',
      maxScore: exam?.maxScore ?? 100,
      examDate: exam?.examDate ?? '',
    },
  })

  const isSaving = createExam.isPending || updateExam.isPending

  async function onSubmit(values: ExamFormValues) {
    if (exam) {
      await updateExam.mutateAsync({ id: exam.id, ...values })
    } else {
      await createExam.mutateAsync(values)
    }
    onClose()
  }

  return (
    <Modal title={exam ? 'Edit exam' : 'New exam'} onClose={onClose}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <Input label="Name" {...register('name')} error={errors.name?.message} />
        <Input
          label="Max score"
          type="number"
          step="0.01"
          {...register('maxScore', { valueAsNumber: true })}
          error={errors.maxScore?.message}
        />
        <Input label="Exam date" type="date" {...register('examDate')} error={errors.examDate?.message} />

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

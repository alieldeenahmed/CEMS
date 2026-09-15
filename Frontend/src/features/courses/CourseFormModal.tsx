import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { useBranches } from '@/features/branches/api'
import { useCurricula } from '@/features/curricula/api'
import { Button } from '@/shared/ui/Button'
import { Select } from '@/shared/ui/Input'
import { Modal } from '@/shared/ui/Modal'
import { Input } from '@/shared/ui/Input'
import { useCreateCourse, useUpdateCourse } from './api'
import type { Course } from './types'

const courseSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  deliveryMode: z.enum(['Group', 'OneOnOne']),
  curriculumId: z.string().min(1, 'Curriculum is required'),
  branchId: z.string().min(1, 'Branch is required'),
})

type CourseFormValues = z.infer<typeof courseSchema>

export function CourseFormModal({ course, onClose }: { course: Course | null; onClose: () => void }) {
  const { data: branches } = useBranches()
  const { data: curricula } = useCurricula()
  const createCourse = useCreateCourse()
  const updateCourse = useUpdateCourse()

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<CourseFormValues>({
    resolver: zodResolver(courseSchema),
    defaultValues: {
      name: course?.name ?? '',
      deliveryMode: course?.deliveryMode ?? 'Group',
      curriculumId: course?.curriculumId ?? '',
      branchId: course?.branchId ?? '',
    },
  })

  const isSaving = createCourse.isPending || updateCourse.isPending

  async function onSubmit(values: CourseFormValues) {
    if (course) {
      await updateCourse.mutateAsync({ id: course.id, name: values.name, deliveryMode: values.deliveryMode })
    } else {
      await createCourse.mutateAsync(values)
    }
    onClose()
  }

  return (
    <Modal title={course ? 'Edit course' : 'New course'} onClose={onClose}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <Input label="Name" {...register('name')} error={errors.name?.message} />

        <Select label="Delivery mode" {...register('deliveryMode')}>
          <option value="Group">Group</option>
          <option value="OneOnOne">One-on-one</option>
        </Select>

        {!course && (
          <>
            <Select label="Curriculum" {...register('curriculumId')} error={errors.curriculumId?.message}>
              <option value="">Select a curriculum</option>
              {curricula?.map((curriculum) => (
                <option key={curriculum.id} value={curriculum.id}>
                  {curriculum.name}
                </option>
              ))}
            </Select>

            <Select label="Branch" {...register('branchId')} error={errors.branchId?.message}>
              <option value="">Select a branch</option>
              {branches?.map((branch) => (
                <option key={branch.id} value={branch.id}>
                  {branch.name}
                </option>
              ))}
            </Select>
          </>
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

import { X } from 'lucide-react'
import { useState } from 'react'
import { useCourses } from '@/features/courses/api'
import { Button } from '@/shared/ui/Button'
import { useAddQualification, useQualificationsForTeacher, useRemoveQualification } from './api'

export function QualificationsSection({ teacherId }: { teacherId: string }) {
  const { data: courses } = useCourses()
  const { data: qualifications, isLoading } = useQualificationsForTeacher(teacherId)
  const addQualification = useAddQualification(teacherId)
  const removeQualification = useRemoveQualification(teacherId)
  const [selectedCourseId, setSelectedCourseId] = useState('')

  const qualifiedCourseIds = new Set(qualifications?.map((q) => q.courseId))
  const availableCourses = courses?.filter((c) => !qualifiedCourseIds.has(c.id)) ?? []

  if (isLoading) {
    return <p className="text-sm text-muted">Loading qualifications...</p>
  }

  return (
    <div>
      <p className="mb-2 text-xs font-medium uppercase tracking-wide text-muted">
        Declared qualifications
      </p>
      <p className="mb-2 text-xs text-muted">
        Which courses this teacher is qualified to teach -- separate from what they're actually
        scheduled for.
      </p>

      <div className="mb-2 flex flex-wrap gap-1.5">
        {qualifications?.map((qualification) => (
          <span
            key={qualification.courseId}
            className="flex items-center gap-1 rounded-full bg-navy/10 px-2.5 py-0.5 text-xs font-medium text-navy"
          >
            {qualification.courseName}
            <button
              type="button"
              aria-label={`Remove qualification for ${qualification.courseName}`}
              onClick={() => removeQualification.mutate(qualification.courseId)}
              className="hover:text-coral"
            >
              <X size={12} />
            </button>
          </span>
        ))}
        {qualifications?.length === 0 && (
          <p className="text-sm text-muted">No declared qualifications yet.</p>
        )}
      </div>

      {availableCourses.length > 0 && (
        <div className="flex items-center gap-2">
          <select
            value={selectedCourseId}
            onChange={(e) => setSelectedCourseId(e.target.value)}
            className="rounded-md border border-line bg-paper px-2 py-1.5 text-sm outline-none focus:border-navy"
          >
            <option value="">Declare qualified for...</option>
            {availableCourses.map((course) => (
              <option key={course.id} value={course.id}>
                {course.name}
              </option>
            ))}
          </select>
          <Button
            variant="secondary"
            className="px-2.5 py-1.5 text-xs"
            disabled={!selectedCourseId}
            onClick={() => {
              addQualification.mutate(selectedCourseId)
              setSelectedCourseId('')
            }}
          >
            Add
          </Button>
        </div>
      )}
    </div>
  )
}

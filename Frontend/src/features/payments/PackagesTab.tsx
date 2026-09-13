import { Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useBranches } from '@/features/branches/api'
import { useCourses } from '@/features/courses/api'
import { getErrorMessage } from '@/shared/api/errors'
import { Button } from '@/shared/ui/Button'
import { Select } from '@/shared/ui/Input'
import { useDeletePackage, usePackagesForCourse } from './api'
import { PackageFormModal } from './PackageFormModal'
import type { Package } from './types'

export function PackagesTab() {
  const { data: courses } = useCourses()
  const { data: branches } = useBranches()
  const [courseId, setCourseId] = useState('')
  const { data: packages, isLoading } = usePackagesForCourse(courseId || null)
  const deletePackage = useDeletePackage(courseId)
  const [modalState, setModalState] = useState<'closed' | 'create' | Package>('closed')

  const branchNameById = new Map(branches?.map((b) => [b.id, b.name]))

  function handleDelete(pkg: Package) {
    if (window.confirm(`Delete this package (${pkg.sessionCount} sessions)? This cannot be undone.`)) {
      deletePackage.mutate(pkg.id, {
        onError: (error) => window.alert(getErrorMessage(error, 'Could not delete this package.')),
      })
    }
  }

  return (
    <div>
      <div className="mb-4 flex items-center gap-3">
        <div className="w-72">
          <Select value={courseId} onChange={(e) => setCourseId(e.target.value)}>
            <option value="">Select a course...</option>
            {courses?.map((course) => (
              <option key={course.id} value={course.id}>
                {course.name} · {branchNameById.get(course.branchId) ?? 'Unknown branch'}
              </option>
            ))}
          </Select>
        </div>
        {courseId && (
          <Button onClick={() => setModalState('create')}>
            <Plus size={15} />
            New package
          </Button>
        )}
      </div>

      {!courseId && <p className="text-sm text-muted">Choose a course to see its packages.</p>}
      {courseId && isLoading && <p className="text-sm text-muted">Loading packages...</p>}

      {courseId && packages && (
        <div className="overflow-hidden rounded-lg border border-line bg-paper">
          {packages.map((pkg, index) => (
            <div
              key={pkg.id}
              className={`flex items-center gap-3 px-4 py-3 ${index > 0 ? 'border-t border-line' : ''}`}
            >
              <p className="flex-1 text-sm text-ink">
                {pkg.sessionCount} sessions · <span className="font-medium">${pkg.price}</span>
              </p>
              <button
                type="button"
                aria-label="Edit package"
                onClick={() => setModalState(pkg)}
                className="text-muted transition-colors hover:text-navy"
              >
                <Pencil size={15} />
              </button>
              <button
                type="button"
                aria-label="Delete package"
                onClick={() => handleDelete(pkg)}
                className="text-muted transition-colors hover:text-coral"
              >
                <Trash2 size={15} />
              </button>
            </div>
          ))}

          {packages.length === 0 && (
            <p className="px-4 py-6 text-center text-sm text-muted">No packages yet.</p>
          )}
        </div>
      )}

      {modalState !== 'closed' && courseId && (
        <PackageFormModal
          courseId={courseId}
          pkg={modalState === 'create' ? null : modalState}
          onClose={() => setModalState('closed')}
        />
      )}
    </div>
  )
}

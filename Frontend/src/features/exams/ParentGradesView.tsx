import { Download } from 'lucide-react'
import { useState } from 'react'
import { useMyChildren } from '@/features/students/api'
import { Button } from '@/shared/ui/Button'
import { Select } from '@/shared/ui/Input'
import { downloadReportCard, useGradesForStudent } from './api'

export function ParentGradesView() {
  const { data: children } = useMyChildren()
  const [studentId, setStudentId] = useState('')
  const { data: grades, isLoading } = useGradesForStudent(studentId || null)

  const selectedChild = children?.find((c) => c.id === studentId) ?? null

  return (
    <div>
      <div className="mb-4 flex items-center gap-3">
        <div className="w-64">
          <Select value={studentId} onChange={(e) => setStudentId(e.target.value)}>
            <option value="">Select a child...</option>
            {children?.map((child) => (
              <option key={child.id} value={child.id}>
                {child.fullName}
              </option>
            ))}
          </Select>
        </div>
        {selectedChild && (
          <Button
            variant="secondary"
            onClick={() => downloadReportCard(selectedChild.id, selectedChild.fullName)}
          >
            <Download size={15} />
            Download report card
          </Button>
        )}
      </div>

      {!selectedChild && <p className="text-sm text-muted">Choose a child to see their grades.</p>}

      {selectedChild && isLoading && <p className="text-sm text-muted">Loading grades...</p>}

      {selectedChild && grades && (
        <div className="overflow-hidden rounded-lg border border-line bg-paper">
          {grades.map((grade, index) => (
            <div
              key={`${grade.examId}-${grade.studentId}`}
              className={`flex items-center justify-between px-4 py-3 ${index > 0 ? 'border-t border-line' : ''}`}
            >
              <p className="text-sm font-medium text-ink">{grade.examName}</p>
              <p className="text-sm text-muted">
                {grade.score != null ? `${grade.score} / ${grade.examMaxScore}` : 'Not graded yet'}
              </p>
            </div>
          ))}

          {grades.length === 0 && (
            <p className="px-4 py-6 text-center text-sm text-muted">No grades recorded yet.</p>
          )}
        </div>
      )}
    </div>
  )
}

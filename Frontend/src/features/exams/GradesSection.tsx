import { useState } from 'react'
import { Link } from 'react-router-dom'
import { getErrorMessage } from '@/shared/api/errors'
import { Button } from '@/shared/ui/Button'
import { useGradesForExam, useRecordGrade } from './api'
import type { Exam, Grade } from './types'

export function GradesSection({ exam, canGrade }: { exam: Exam; canGrade: boolean }) {
  const { data: grades, isLoading, isError } = useGradesForExam(exam.id)
  const recordGrade = useRecordGrade(exam.id)

  if (isLoading) {
    return <p className="text-sm text-muted">Loading grades...</p>
  }

  if (isError) {
    return <p className="text-sm text-coral">Could not load grades for this exam.</p>
  }

  return (
    <div className="border-t border-line bg-parchment/50 p-4">
      <p className="mb-3 text-xs font-medium uppercase tracking-wide text-muted">
        Grades (out of {exam.maxScore})
      </p>

      <div className="space-y-1.5">
        {grades?.map((grade) => (
          <GradeRow
            key={grade.studentId}
            grade={grade}
            maxScore={exam.maxScore}
            canGrade={canGrade}
            onSave={(score, comments) =>
              recordGrade.mutate(
                { studentId: grade.studentId, score, comments },
                { onError: (error) => window.alert(getErrorMessage(error, 'Could not save this grade.')) },
              )
            }
          />
        ))}

        {grades?.length === 0 && <p className="text-sm text-muted">No students enrolled in this course.</p>}
      </div>
    </div>
  )
}

function GradeRow({
  grade,
  maxScore,
  canGrade,
  onSave,
}: {
  grade: Grade
  maxScore: number
  canGrade: boolean
  onSave: (score: number, comments: string | null) => void
}) {
  const [score, setScore] = useState(grade.score?.toString() ?? '')
  const [comments, setComments] = useState(grade.comments ?? '')

  const isDirty = score !== (grade.score?.toString() ?? '') || comments !== (grade.comments ?? '')

  return (
    <div className="flex items-center gap-2 rounded-md bg-paper px-3 py-2 text-sm">
      <Link to={`/students/${grade.studentId}`} className="flex-1 text-ink hover:text-navy hover:underline">
        {grade.studentFullName}
      </Link>

      <input
        type="number"
        min={0}
        max={maxScore}
        step="0.01"
        placeholder="Score"
        value={score}
        disabled={!canGrade}
        onChange={(e) => setScore(e.target.value)}
        className="w-20 rounded-md border border-line px-2 py-1 text-sm outline-none focus:border-navy disabled:opacity-60"
      />
      <input
        type="text"
        placeholder="Comments"
        value={comments}
        disabled={!canGrade}
        onChange={(e) => setComments(e.target.value)}
        className="flex-1 rounded-md border border-line px-2 py-1 text-sm outline-none focus:border-navy disabled:opacity-60"
      />

      {canGrade && (
        <Button
          variant="secondary"
          className="px-2.5 py-1 text-xs"
          disabled={!isDirty || score === ''}
          onClick={() => onSave(Number(score), comments.trim() === '' ? null : comments)}
        >
          Save
        </Button>
      )}
    </div>
  )
}

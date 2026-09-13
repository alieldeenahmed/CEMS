export interface Exam {
  id: string
  courseId: string
  name: string
  maxScore: number
  examDate: string
}

export interface ExamInput {
  name: string
  maxScore: number
  examDate: string
}

export interface Grade {
  id: string | null
  examId: string
  examName: string
  examMaxScore: number
  studentId: string
  studentFullName: string
  score: number | null
  comments: string | null
  gradedAtUtc: string | null
  gradedByUserId: string | null
}

export interface RecordGradeInput {
  studentId: string
  score: number
  comments: string | null
}

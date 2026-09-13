import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/shared/api/client'
import type { Exam, ExamInput, Grade, RecordGradeInput } from './types'

export function useExamsForCourse(courseId: string | null) {
  return useQuery({
    queryKey: ['exams', courseId],
    queryFn: async () => {
      const { data } = await apiClient.get<Exam[]>(`/courses/${courseId}/exams`)
      return data
    },
    enabled: !!courseId,
  })
}

export function useCreateExam(courseId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: ExamInput) => {
      const { data } = await apiClient.post<Exam>(`/courses/${courseId}/exams`, input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['exams', courseId] }),
  })
}

export function useUpdateExam(courseId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, ...input }: ExamInput & { id: string }) => {
      const { data } = await apiClient.put<Exam>(`/exams/${id}`, input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['exams', courseId] }),
  })
}

export function useDeleteExam(courseId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (id: string) => {
      await apiClient.delete(`/exams/${id}`)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['exams', courseId] }),
  })
}

export function useGradesForExam(examId: string | null) {
  return useQuery({
    queryKey: ['exam-grades', examId],
    queryFn: async () => {
      const { data } = await apiClient.get<Grade[]>(`/exams/${examId}/grades`)
      return data
    },
    enabled: !!examId,
  })
}

export function useRecordGrade(examId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: RecordGradeInput) => {
      const { data } = await apiClient.post<Grade>(`/exams/${examId}/grades`, input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['exam-grades', examId] }),
  })
}


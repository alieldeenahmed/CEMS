import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/shared/api/client'
import type { Course, CourseInput, Enrollment, UpdateCourseInput } from './types'

export function useCourses() {
  return useQuery({
    queryKey: ['courses'],
    queryFn: async () => {
      const { data } = await apiClient.get<Course[]>('/courses')
      return data
    },
  })
}

export function useMyCourses() {
  return useQuery({
    queryKey: ['my-courses'],
    queryFn: async () => {
      const { data } = await apiClient.get<Course[]>('/courses/my-courses')
      return data
    },
  })
}

export function useCreateCourse() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CourseInput) => {
      const { data } = await apiClient.post<Course>('/courses', input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['courses'] }),
  })
}

export function useUpdateCourse() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, ...input }: UpdateCourseInput & { id: string }) => {
      const { data } = await apiClient.put<Course>(`/courses/${id}`, input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['courses'] }),
  })
}

export function useDeleteCourse() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (id: string) => {
      await apiClient.delete(`/courses/${id}`)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['courses'] }),
  })
}

export function useEnrollmentsForCourse(courseId: string | null) {
  return useQuery({
    queryKey: ['enrollments', courseId],
    queryFn: async () => {
      const { data } = await apiClient.get<Enrollment[]>(`/courses/${courseId}/enrollments`)
      return data
    },
    enabled: !!courseId,
  })
}

export function useEnrollStudent(courseId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (studentId: string) => {
      const { data } = await apiClient.post<Enrollment>(`/courses/${courseId}/enrollments`, { studentId })
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['enrollments', courseId] }),
  })
}

export function useDropEnrollment(courseId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (enrollmentId: string) => {
      await apiClient.delete(`/courses/enrollments/${enrollmentId}`)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['enrollments', courseId] }),
  })
}

export function usePromoteFromWaitlist(courseId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (enrollmentId: string) => {
      await apiClient.post(`/courses/enrollments/${enrollmentId}/promote`)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['enrollments', courseId] }),
  })
}

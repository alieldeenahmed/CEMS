import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/shared/api/client'
import type { CourseSession, CreateSessionInput } from './types'

export function useSession(sessionId: string | null) {
  return useQuery({
    queryKey: ['session', sessionId],
    queryFn: async () => {
      const { data } = await apiClient.get<CourseSession>(`/sessions/${sessionId}`)
      return data
    },
    enabled: !!sessionId,
  })
}

export function useSessionsForCourse(courseId: string | null) {
  return useQuery({
    queryKey: ['sessions', courseId],
    queryFn: async () => {
      const { data } = await apiClient.get<CourseSession[]>(`/courses/${courseId}/sessions`)
      return data
    },
    enabled: !!courseId,
  })
}

export function useCreateSession(courseId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateSessionInput) => {
      const { data } = await apiClient.post<CourseSession>(`/courses/${courseId}/sessions`, input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sessions', courseId] }),
  })
}

export function useCancelSession(courseId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, rescheduledToSessionId }: { id: string; rescheduledToSessionId: string | null }) => {
      const { data } = await apiClient.post<CourseSession>(`/sessions/${id}/cancel`, {
        rescheduledToSessionId,
      })
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sessions', courseId] }),
  })
}

export function useSubstituteSessionTeacher(courseId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({
      id,
      newTeacherId,
      override,
      overrideReason,
    }: {
      id: string
      newTeacherId: string
      override: boolean
      overrideReason: string | null
    }) => {
      const { data } = await apiClient.post<CourseSession>(`/sessions/${id}/substitute-teacher`, {
        newTeacherId,
        override,
        overrideReason,
      })
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sessions', courseId] }),
  })
}

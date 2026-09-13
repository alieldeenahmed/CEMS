import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/shared/api/client'
import type { Curriculum, CurriculumInput, Subject, SubjectInput } from './types'

export function useCurricula() {
  return useQuery({
    queryKey: ['curricula'],
    queryFn: async () => {
      const { data } = await apiClient.get<Curriculum[]>('/curricula')
      return data
    },
  })
}

export function useCreateCurriculum() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CurriculumInput) => {
      const { data } = await apiClient.post<Curriculum>('/curricula', input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['curricula'] }),
  })
}

export function useUpdateCurriculum() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, ...input }: CurriculumInput & { id: string }) => {
      const { data } = await apiClient.put<Curriculum>(`/curricula/${id}`, input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['curricula'] }),
  })
}

export function useDeleteCurriculum() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (id: string) => {
      await apiClient.delete(`/curricula/${id}`)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['curricula'] }),
  })
}

export function useSubjects(curriculumId?: string) {
  return useQuery({
    queryKey: ['subjects', curriculumId ?? 'all'],
    queryFn: async () => {
      const { data } = await apiClient.get<Subject[]>('/subjects', {
        params: curriculumId ? { curriculumId } : undefined,
      })
      return data
    },
  })
}

export function useCreateSubject() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: SubjectInput) => {
      const { data } = await apiClient.post<Subject>('/subjects', input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['subjects'] }),
  })
}

export function useUpdateSubject() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, name }: { id: string; name: string }) => {
      const { data } = await apiClient.put<Subject>(`/subjects/${id}`, { name })
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['subjects'] }),
  })
}

export function useDeleteSubject() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (id: string) => {
      await apiClient.delete(`/subjects/${id}`)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['subjects'] }),
  })
}

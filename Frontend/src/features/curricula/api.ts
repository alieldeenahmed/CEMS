import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/shared/api/client'
import type { Curriculum, CurriculumInput } from './types'

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

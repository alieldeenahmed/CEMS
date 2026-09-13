import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/shared/api/client'
import type { Branch, BranchInput, Room, RoomInput } from './types'

export function useBranches() {
  return useQuery({
    queryKey: ['branches'],
    queryFn: async () => {
      const { data } = await apiClient.get<Branch[]>('/branches')
      return data
    },
  })
}

export function useCreateBranch() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: BranchInput) => {
      const { data } = await apiClient.post<Branch>('/branches', input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['branches'] }),
  })
}

export function useUpdateBranch() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, ...input }: BranchInput & { id: string; isActive: boolean }) => {
      const { data } = await apiClient.put<Branch>(`/branches/${id}`, input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['branches'] }),
  })
}

export function useRoomsByBranch(branchId: string | null) {
  return useQuery({
    queryKey: ['rooms', branchId],
    queryFn: async () => {
      const { data } = await apiClient.get<Room[]>(`/branches/${branchId}/rooms`)
      return data
    },
    enabled: !!branchId,
  })
}

export function useCreateRoom(branchId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: RoomInput) => {
      const { data } = await apiClient.post<Room>(`/branches/${branchId}/rooms`, input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['rooms', branchId] }),
  })
}

export function useUpdateRoom(branchId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, ...input }: RoomInput & { id: string }) => {
      const { data } = await apiClient.put<Room>(`/rooms/${id}`, input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['rooms', branchId] }),
  })
}

export function useDeleteRoom(branchId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (id: string) => {
      await apiClient.delete(`/rooms/${id}`)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['rooms', branchId] }),
  })
}

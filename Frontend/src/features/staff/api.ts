import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/shared/api/client'
import type { CreateStaffUserInput, StaffUser } from './types'

export function useStaffUsers(options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: ['staff'],
    queryFn: async () => {
      const { data } = await apiClient.get<StaffUser[]>('/users/staff')
      return data
    },
    enabled: options?.enabled,
  })
}

export function useBranchStaff(options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: ['branch-staff'],
    queryFn: async () => {
      const { data } = await apiClient.get<StaffUser[]>('/users/staff/my-branch')
      return data
    },
    enabled: options?.enabled,
  })
}

export function useCreateStaffUser() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateStaffUserInput) => {
      const { data } = await apiClient.post<StaffUser>('/users/staff', input)
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['staff'] })
      queryClient.invalidateQueries({ queryKey: ['branch-staff'] })
    },
  })
}

export function useResetStaffPassword() {
  return useMutation({
    mutationFn: async ({ userId, newPassword }: { userId: string; newPassword: string }) => {
      await apiClient.post(`/users/staff/${userId}/reset-password`, { newPassword })
    },
  })
}

export function useSetStaffUserActive() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ userId, isActive }: { userId: string; isActive: boolean }) => {
      await apiClient.put(`/users/staff/${userId}/status`, { isActive })
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['staff'] }),
  })
}

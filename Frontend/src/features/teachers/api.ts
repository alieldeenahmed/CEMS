import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/shared/api/client'
import type { CourseSession } from '@/features/scheduling/types'
import type { AddAvailabilityInput, CreateTeacherInput, Teacher, TeacherAvailability, UpdateTeacherInput } from './types'

export function useMySchedule() {
  return useQuery({
    queryKey: ['my-schedule'],
    queryFn: async () => {
      const { data } = await apiClient.get<CourseSession[]>('/teachers/my-schedule')
      return data
    },
  })
}

export function useTeachers() {
  return useQuery({
    queryKey: ['teachers'],
    queryFn: async () => {
      const { data } = await apiClient.get<Teacher[]>('/teachers')
      return data
    },
  })
}

export function useCreateTeacherProfile() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateTeacherInput) => {
      const { data } = await apiClient.post<Teacher>('/teachers', input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['teachers'] }),
  })
}

export function useUpdateTeacher() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, ...input }: UpdateTeacherInput & { id: string }) => {
      const { data } = await apiClient.put<Teacher>(`/teachers/${id}`, input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['teachers'] }),
  })
}

export function useDeleteTeacher() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (id: string) => {
      await apiClient.delete(`/teachers/${id}`)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['teachers'] }),
  })
}

export function useAddTeacherToBranch(teacherId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (branchId: string) => {
      await apiClient.post(`/teachers/${teacherId}/branches`, { branchId })
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['teachers'] }),
  })
}

export function useRemoveTeacherFromBranch(teacherId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (branchId: string) => {
      await apiClient.delete(`/teachers/${teacherId}/branches/${branchId}`)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['teachers'] }),
  })
}

export function useAvailabilityForTeacher(teacherId: string | null) {
  return useQuery({
    queryKey: ['teacher-availability', teacherId],
    queryFn: async () => {
      const { data } = await apiClient.get<TeacherAvailability[]>(`/teachers/${teacherId}/availability`)
      return data
    },
    enabled: !!teacherId,
  })
}

export function useAddAvailability(teacherId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: AddAvailabilityInput) => {
      const { data } = await apiClient.post<TeacherAvailability>(`/teachers/${teacherId}/availability`, input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['teacher-availability', teacherId] }),
  })
}

export function useRemoveAvailability(teacherId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (availabilityId: string) => {
      await apiClient.delete(`/teachers/availability/${availabilityId}`)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['teacher-availability', teacherId] }),
  })
}

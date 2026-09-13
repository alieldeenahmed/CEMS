import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/shared/api/client'
import type {
  CreateGuardianInput,
  CreateStudentInput,
  Guardian,
  LinkGuardianInput,
  Student,
  UpdateStudentInput,
} from './types'

export function useStudents() {
  return useQuery({
    queryKey: ['students'],
    queryFn: async () => {
      const { data } = await apiClient.get<Student[]>('/students')
      return data
    },
  })
}

export function useCreateStudent() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateStudentInput) => {
      const { data } = await apiClient.post<Student>('/students', input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['students'] }),
  })
}

export function useUpdateStudent() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, ...input }: UpdateStudentInput & { id: string }) => {
      const { data } = await apiClient.put<Student>(`/students/${id}`, input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['students'] }),
  })
}

export function useDeleteStudent() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (id: string) => {
      await apiClient.delete(`/students/${id}`)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['students'] }),
  })
}

export function useGuardiansForStudent(studentId: string | null) {
  return useQuery({
    queryKey: ['student-guardians', studentId],
    queryFn: async () => {
      const { data } = await apiClient.get<Guardian[]>(`/students/${studentId}/guardians`)
      return data
    },
    enabled: !!studentId,
  })
}

export function useAllGuardians() {
  return useQuery({
    queryKey: ['guardians'],
    queryFn: async () => {
      const { data } = await apiClient.get<Guardian[]>('/guardians')
      return data
    },
  })
}

export function useCreateGuardian() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateGuardianInput) => {
      const { data } = await apiClient.post<Guardian>('/guardians', input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['guardians'] }),
  })
}

export function useLinkGuardian(studentId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: LinkGuardianInput) => {
      await apiClient.post(`/students/${studentId}/guardians`, input)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['student-guardians', studentId] }),
  })
}

export function useUnlinkGuardian(studentId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (guardianId: string) => {
      await apiClient.delete(`/students/${studentId}/guardians/${guardianId}`)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['student-guardians', studentId] }),
  })
}

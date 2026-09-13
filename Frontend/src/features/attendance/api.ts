import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/shared/api/client'
import type { AttendanceRecord, AttendanceStatus } from './types'

export function useAttendanceForSession(sessionId: string | null) {
  return useQuery({
    queryKey: ['session-attendance', sessionId],
    queryFn: async () => {
      const { data } = await apiClient.get<AttendanceRecord[]>(`/sessions/${sessionId}/attendance`)
      return data
    },
    enabled: !!sessionId,
  })
}

export function useAttendanceForStudent(studentId: string | null) {
  return useQuery({
    queryKey: ['student-attendance', studentId],
    queryFn: async () => {
      const { data } = await apiClient.get<AttendanceRecord[]>(`/students/${studentId}/attendance`)
      return data
    },
    enabled: !!studentId,
  })
}

export function useMarkAttendance(sessionId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ studentId, status }: { studentId: string; status: AttendanceStatus }) => {
      const { data } = await apiClient.post<AttendanceRecord>(`/sessions/${sessionId}/attendance`, {
        studentId,
        status,
      })
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['session-attendance', sessionId] }),
  })
}

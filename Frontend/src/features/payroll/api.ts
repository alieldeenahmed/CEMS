import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/shared/api/client'
import type { GeneratePayrollRunInput, PayrollLineItem, PayrollRun } from './types'

export function usePayrollRunsForTeacher(teacherId: string | null) {
  return useQuery({
    queryKey: ['payroll-runs', teacherId],
    queryFn: async () => {
      const { data } = await apiClient.get<PayrollRun[]>(`/teachers/${teacherId}/payroll-runs`)
      return data
    },
    enabled: !!teacherId,
  })
}

export function useMyPayrollRuns() {
  return useQuery({
    queryKey: ['my-payroll-runs'],
    queryFn: async () => {
      const { data } = await apiClient.get<PayrollRun[]>('/teachers/my-payroll-runs')
      return data
    },
  })
}

export function useGeneratePayrollRun(teacherId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: GeneratePayrollRunInput) => {
      const { data } = await apiClient.post<PayrollRun>(`/teachers/${teacherId}/payroll-runs`, input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['payroll-runs', teacherId] }),
  })
}

function invalidateRun(queryClient: ReturnType<typeof useQueryClient>, teacherId: string) {
  queryClient.invalidateQueries({ queryKey: ['payroll-runs', teacherId] })
  queryClient.invalidateQueries({ queryKey: ['my-payroll-runs'] })
}

export function useApprovePayrollRun(teacherId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (runId: string) => {
      const { data } = await apiClient.post<PayrollRun>(`/payroll-runs/${runId}/approve`)
      return data
    },
    onSuccess: () => invalidateRun(queryClient, teacherId),
  })
}

export function useMarkPayrollRunPaid(teacherId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (runId: string) => {
      const { data } = await apiClient.post<PayrollRun>(`/payroll-runs/${runId}/mark-paid`)
      return data
    },
    onSuccess: () => invalidateRun(queryClient, teacherId),
  })
}

export function useLineItemsForPayrollRun(runId: string | null) {
  return useQuery({
    queryKey: ['payroll-line-items', runId],
    queryFn: async () => {
      const { data } = await apiClient.get<PayrollLineItem[]>(`/payroll-runs/${runId}/line-items`)
      return data
    },
    enabled: !!runId,
  })
}

export async function downloadPayStub(runId: string, periodStart: string, periodEnd: string) {
  const response = await apiClient.get(`/payroll-runs/${runId}/paystub`, { responseType: 'blob' })
  const url = URL.createObjectURL(response.data)
  const link = document.createElement('a')
  link.href = url
  link.download = `paystub-${periodStart}-to-${periodEnd}.pdf`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
}

import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/shared/api/client'
import type { DashboardSummary } from '@/features/dashboard/types'

export function useAnalyticsDashboard(branchId: string | undefined, periodStart: string, periodEnd: string) {
  return useQuery({
    queryKey: ['analytics-dashboard', branchId, periodStart, periodEnd],
    queryFn: async () => {
      const { data } = await apiClient.get<DashboardSummary>('/analytics/dashboard', {
        params: { branchId, periodStart, periodEnd },
      })
      return data
    },
    enabled: !!periodStart && !!periodEnd,
  })
}

async function downloadDashboardFile(
  format: 'pdf' | 'excel',
  branchId: string | undefined,
  periodStart: string,
  periodEnd: string,
) {
  const response = await apiClient.get(`/analytics/dashboard/export/${format}`, {
    params: { branchId, periodStart, periodEnd },
    responseType: 'blob',
  })
  const url = URL.createObjectURL(response.data)
  const link = document.createElement('a')
  link.href = url
  link.download = `dashboard-${periodStart}-to-${periodEnd}.${format === 'excel' ? 'xlsx' : 'pdf'}`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
}

export const downloadDashboardPdf = (branchId: string | undefined, periodStart: string, periodEnd: string) =>
  downloadDashboardFile('pdf', branchId, periodStart, periodEnd)

export const downloadDashboardExcel = (branchId: string | undefined, periodStart: string, periodEnd: string) =>
  downloadDashboardFile('excel', branchId, periodStart, periodEnd)

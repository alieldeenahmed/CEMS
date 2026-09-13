import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/shared/api/client'
import type { DashboardSummary } from './types'

function currentMonthRange() {
  const now = new Date()
  const periodStart = new Date(now.getFullYear(), now.getMonth(), 1)
  const periodEnd = new Date(now.getFullYear(), now.getMonth() + 1, 0)
  const toDateOnly = (date: Date) => date.toISOString().slice(0, 10)
  return { periodStart: toDateOnly(periodStart), periodEnd: toDateOnly(periodEnd) }
}

export function useDashboardSummary(branchId: string | undefined) {
  const { periodStart, periodEnd } = currentMonthRange()

  return useQuery({
    queryKey: ['dashboard-summary', branchId, periodStart, periodEnd],
    queryFn: async () => {
      const { data } = await apiClient.get<DashboardSummary>('/analytics/dashboard', {
        params: { branchId, periodStart, periodEnd },
      })
      return data
    },
  })
}

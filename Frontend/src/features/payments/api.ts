import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/shared/api/client'
import type { CreateInvoiceInput, Invoice, Package, PackageInput, Payment, RecordPaymentInput } from './types'

export function usePackagesForCourse(courseId: string | null) {
  return useQuery({
    queryKey: ['packages', courseId],
    queryFn: async () => {
      const { data } = await apiClient.get<Package[]>(`/courses/${courseId}/packages`)
      return data
    },
    enabled: !!courseId,
  })
}

export function useCreatePackage(courseId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: PackageInput) => {
      const { data } = await apiClient.post<Package>(`/courses/${courseId}/packages`, input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['packages', courseId] }),
  })
}

export function useUpdatePackage(courseId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, ...input }: PackageInput & { id: string }) => {
      const { data } = await apiClient.put<Package>(`/packages/${id}`, input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['packages', courseId] }),
  })
}

export function useDeletePackage(courseId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (id: string) => {
      await apiClient.delete(`/packages/${id}`)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['packages', courseId] }),
  })
}

export function useInvoicesForStudent(studentId: string | null) {
  return useQuery({
    queryKey: ['invoices', studentId],
    queryFn: async () => {
      const { data } = await apiClient.get<Invoice[]>(`/students/${studentId}/invoices`)
      return data
    },
    enabled: !!studentId,
  })
}

export function useCreateInvoice(studentId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateInvoiceInput) => {
      const { data } = await apiClient.post<Invoice>(`/students/${studentId}/invoices`, input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['invoices', studentId] }),
  })
}

export function usePaymentsForInvoice(invoiceId: string | null) {
  return useQuery({
    queryKey: ['payments', invoiceId],
    queryFn: async () => {
      const { data } = await apiClient.get<Payment[]>(`/invoices/${invoiceId}/payments`)
      return data
    },
    enabled: !!invoiceId,
  })
}

export function useRecordPayment(invoiceId: string, studentId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: RecordPaymentInput) => {
      const { data } = await apiClient.post<Payment>(`/invoices/${invoiceId}/payments`, input)
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['payments', invoiceId] })
      queryClient.invalidateQueries({ queryKey: ['invoices', studentId] })
      queryClient.invalidateQueries({ queryKey: ['balance', studentId] })
    },
  })
}

export function useCancelInvoice(studentId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (invoiceId: string) => {
      const { data } = await apiClient.post<Invoice>(`/invoices/${invoiceId}/cancel`)
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['invoices', studentId] })
      queryClient.invalidateQueries({ queryKey: ['balance', studentId] })
    },
  })
}

export type PayrollRunStatus = 'Draft' | 'Approved' | 'Paid'

export interface PayrollRun {
  id: string
  teacherId: string
  periodStart: string
  periodEnd: string
  totalAmount: number
  status: PayrollRunStatus
}

export interface GeneratePayrollRunInput {
  periodStart: string
  periodEnd: string
}

export interface PayrollLineItem {
  id: string
  payrollRunId: string
  courseSessionId: string
  amount: number
}

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

export interface StaffPayrollRun {
  id: string
  userId: string
  periodStart: string
  periodEnd: string
  amount: number
  status: PayrollRunStatus
}

export interface GenerateStaffPayrollRunInput {
  periodStart: string
  periodEnd: string
  amount: number
}

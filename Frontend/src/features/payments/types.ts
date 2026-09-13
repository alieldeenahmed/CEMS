export type InvoiceStatus = 'Pending' | 'PartiallyPaid' | 'Paid' | 'Cancelled'
export type PaymentMethod = 'Cash' | 'Card' | 'Transfer'

export interface Package {
  id: string
  courseId: string
  sessionCount: number
  price: number
}

export interface PackageInput {
  sessionCount: number
  price: number
}

export interface Invoice {
  id: string
  studentId: string
  packageId: string | null
  amount: number
  amountPaid: number
  balanceRemaining: number
  issuedDate: string
  dueDate: string
  status: InvoiceStatus
  isOverdue: boolean
}

export interface CreateInvoiceInput {
  packageId: string | null
  amount: number | null
  dueDate: string
}

export interface Payment {
  id: string
  invoiceId: string
  amountPaid: number
  paymentDate: string
  method: PaymentMethod
  receivedByUserId: string
}

export interface RecordPaymentInput {
  amountPaid: number
  paymentDate: string
  method: PaymentMethod
}

export interface OutstandingBalance {
  studentId: string
  totalOutstanding: number
}

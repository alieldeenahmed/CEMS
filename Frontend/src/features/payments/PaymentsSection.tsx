import { useForm } from 'react-hook-form'
import { getErrorMessage } from '@/shared/api/errors'
import { Button } from '@/shared/ui/Button'
import { Input, Select } from '@/shared/ui/Input'
import { usePaymentsForInvoice, useRecordPayment } from './api'
import type { PaymentMethod } from './types'

interface RecordPaymentFormValues {
  amountPaid: number
  paymentDate: string
  method: PaymentMethod
}

export function PaymentsSection({
  invoiceId,
  studentId,
  canRecordPayment,
}: {
  invoiceId: string
  studentId: string
  canRecordPayment: boolean
}) {
  const { data: payments, isLoading } = usePaymentsForInvoice(invoiceId)
  const recordPayment = useRecordPayment(invoiceId, studentId)

  const { register, handleSubmit, reset } = useForm<RecordPaymentFormValues>({
    defaultValues: {
      paymentDate: new Date().toISOString().slice(0, 10),
      method: 'Cash',
    },
  })

  async function onSubmit(values: RecordPaymentFormValues) {
    try {
      await recordPayment.mutateAsync({
        amountPaid: Number(values.amountPaid),
        paymentDate: values.paymentDate,
        method: values.method,
      })
      reset({ paymentDate: new Date().toISOString().slice(0, 10), method: 'Cash' })
    } catch (error) {
      window.alert(getErrorMessage(error, 'Could not record this payment.'))
    }
  }

  if (isLoading) {
    return <p className="text-sm text-muted">Loading payments...</p>
  }

  return (
    <div className="border-t border-line bg-parchment/50 p-4">
      <p className="mb-3 text-xs font-medium uppercase tracking-wide text-muted">Payments</p>

      {payments && payments.length > 0 ? (
        <ul className="mb-4 space-y-1.5">
          {payments.map((payment) => (
            <li key={payment.id} className="rounded-md bg-paper px-3 py-2 text-sm text-ink">
              ${payment.amountPaid} · {payment.method} · {payment.paymentDate}
            </li>
          ))}
        </ul>
      ) : (
        <p className="mb-4 text-sm text-muted">No payments recorded yet.</p>
      )}

      {canRecordPayment && (
        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-wrap items-end gap-2">
          <div className="w-28">
            <Input
              type="number"
              step="0.01"
              placeholder="Amount"
              {...register('amountPaid', { required: true, valueAsNumber: true })}
            />
          </div>
          <input type="date" {...register('paymentDate', { required: true })} className="rounded-md border border-line bg-paper px-2 py-1.5 text-sm outline-none focus:border-navy" />
          <Select {...register('method')} className="w-32">
            <option value="Cash">Cash</option>
            <option value="Card">Card</option>
            <option value="Transfer">Transfer</option>
          </Select>
          <Button type="submit" variant="secondary" disabled={recordPayment.isPending}>
            Record payment
          </Button>
        </form>
      )}
    </div>
  )
}

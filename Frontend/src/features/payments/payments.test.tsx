import { screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mockApi, renderApp, respond } from '@/test/harness'
import { PaymentsPage } from './PaymentsPage'

const SMOUHA = { id: 'b1', name: 'CodeCamp Smouha', address: '', phone: '', isActive: true }
const python = { id: 'c1', name: 'Python Fundamentals', deliveryMode: 'Group', curriculumId: 'cu', branchId: SMOUHA.id }
const khaled = { id: 's1', fullName: 'Khaled Hany', dateOfBirth: '2013-04-12', gender: 'Male', enrollmentDate: '', status: 'Active', currentBranchId: SMOUHA.id }

const invoice = (id: string, status: string, amount: number, paid: number, extra = {}) => ({
  id, studentId: 's1', packageId: null, amount, amountPaid: paid, balanceRemaining: amount - paid,
  issuedDate: '2030-01-01', dueDate: '2030-02-01', status, isOverdue: false, ...extra,
})
const pending = invoice('i1', 'Pending', 2400, 0)
const partial = invoice('i2', 'PartiallyPaid', 3600, 1200, { isOverdue: true })
const paid = invoice('i3', 'Paid', 500, 500)
const cancelled = invoice('i4', 'Cancelled', 100, 0)

const routes = {
  'GET /students': [khaled],
  'GET /students/s1/invoices': [pending, partial, paid, cancelled],
  'GET /courses': [python],
  'GET /branches': [SMOUHA],
}

afterEach(() => vi.restoreAllMocks())

async function pickStudent(user: ReturnType<typeof renderApp>['user']) {
  await screen.findByRole('option', { name: 'Khaled Hany' })
  await user.selectOptions(screen.getByRole('combobox'), 's1')
}

describe('invoices', () => {
  it('asks for a student first', async () => {
    mockApi(routes)
    renderApp(<PaymentsPage />)

    expect(await screen.findByText('Choose a student to see their invoices.')).toBeInTheDocument()
  })

  it('lists invoices with amounts, balance, status, and an overdue note', async () => {
    mockApi(routes)
    const { user } = renderApp(<PaymentsPage />)

    await pickStudent(user)

    expect(await screen.findByText('$2400 · due 2030-02-01')).toBeInTheDocument()
    expect(screen.getByText('Paid $1200 · balance $2400 · overdue')).toBeInTheDocument()
    expect(screen.getByText('PartiallyPaid')).toBeInTheDocument()
    expect(screen.getByText('Cancelled')).toBeInTheDocument()
  })

  it('shows an empty state and a load failure', async () => {
    mockApi({ ...routes, 'GET /students/s1/invoices': [] })
    const first = renderApp(<PaymentsPage />)
    await pickStudent(first.user)
    expect(await screen.findByText('No invoices yet.')).toBeInTheDocument()
    first.unmount()

    mockApi({ ...routes, 'GET /students/s1/invoices': respond(500) })
    const second = renderApp(<PaymentsPage />)
    await pickStudent(second.user)
    expect(await screen.findByText('Could not load invoices.')).toBeInTheDocument()
  })

  it('offers cancel only to managers, and only for invoices that are neither paid nor cancelled', async () => {
    mockApi(routes)
    const { user } = renderApp(<PaymentsPage />, { roles: ['BranchManager'], branchIds: [SMOUHA.id] })
    await pickStudent(user)
    await screen.findByText('$2400 · due 2030-02-01')

    expect(screen.getAllByRole('button', { name: 'Cancel' })).toHaveLength(2)   // pending and partially paid
  })

  it('does not offer the front desk cancellation or the Packages tab, but does let them record payments', async () => {
    mockApi({ ...routes, 'GET /invoices/i1/payments': [] })
    const { user } = renderApp(<PaymentsPage />, { roles: ['FrontDesk'], branchIds: [SMOUHA.id] })
    await pickStudent(user)
    await screen.findByText('$2400 · due 2030-02-01')

    expect(screen.queryByRole('button', { name: 'Cancel' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Packages' })).not.toBeInTheDocument()

    await user.click(screen.getAllByLabelText('Expand')[0])
    expect(await screen.findByRole('button', { name: 'Record payment' })).toBeInTheDocument()
  })

  it('cancels an invoice after confirmation, and explains a refusal', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const alert = vi.spyOn(window, 'alert').mockImplementation(() => {})
    const api = mockApi({
      ...routes,
      'POST /invoices/i1/cancel': { ...pending, status: 'Cancelled' },
      'POST /invoices/i2/cancel': respond(403, { title: 'Only an Owner or Branch Manager can cancel.' }),
    })
    const { user } = renderApp(<PaymentsPage />, { roles: ['Owner'] })
    await pickStudent(user)
    await screen.findByText('$2400 · due 2030-02-01')

    const cancels = screen.getAllByRole('button', { name: 'Cancel' })
    await user.click(cancels[0])
    await waitFor(() => expect(api.made('POST', 'i1/cancel')).toHaveLength(1))

    await user.click(cancels[1])
    await waitFor(() => expect(alert).toHaveBeenCalledWith('Only an Owner or Branch Manager can cancel.'))
  })

  describe('recording payments', () => {
    const open = async (options: Parameters<typeof renderApp>[1] = {}) => {
      const view = renderApp(<PaymentsPage />, options)
      await pickStudent(view.user)
      await screen.findByText('$2400 · due 2030-02-01')
      await view.user.click(screen.getAllByLabelText('Expand')[0])
      await screen.findByText('Payments', { selector: 'p' })
      return view
    }

    it('lists the payments already recorded', async () => {
      mockApi({ ...routes, 'GET /invoices/i1/payments': [{ id: 'p1', invoiceId: 'i1', amountPaid: 600, paymentDate: '2030-01-10', method: 'Cash', receivedByUserId: 'u' }] })
      await open()

      expect(await screen.findByText('$600 · Cash · 2030-01-10')).toBeInTheDocument()
    })

    it('says when there are none', async () => {
      mockApi({ ...routes, 'GET /invoices/i1/payments': [] })
      await open()

      expect(await screen.findByText('No payments recorded yet.')).toBeInTheDocument()
    })

    it('records a payment with its amount, date and method', async () => {
      const api = mockApi({ ...routes, 'GET /invoices/i1/payments': [], 'POST /invoices/i1/payments': { id: 'p9' } })
      const { user } = await open()

      await user.type(screen.getByPlaceholderText('Amount'), '600')
      await user.selectOptions(screen.getByDisplayValue('Cash'), 'Card')
      await user.click(screen.getByRole('button', { name: 'Record payment' }))

      await waitFor(() => expect(api.made('POST', 'payments')).toHaveLength(1))
      expect(api.made('POST', 'payments')[0].body).toMatchObject({ amountPaid: 600, method: 'Card' })
      expect(api.made('POST', 'payments')[0].body.paymentDate).toMatch(/^\d{4}-\d{2}-\d{2}$/)
    })

    it('reports a payment the server refuses', async () => {
      const alert = vi.spyOn(window, 'alert').mockImplementation(() => {})
      mockApi({ ...routes, 'GET /invoices/i1/payments': [], 'POST /invoices/i1/payments': respond(400, { title: 'Bad request', errors: ['Cannot record a payment against a cancelled invoice.'] }) })
      const { user } = await open()

      await user.type(screen.getByPlaceholderText('Amount'), '10')
      await user.click(screen.getByRole('button', { name: 'Record payment' }))

      await waitFor(() => expect(alert).toHaveBeenCalledWith('Cannot record a payment against a cancelled invoice.'))
    })

    it('hides the payment form for a cancelled invoice', async () => {
      mockApi({ ...routes, 'GET /invoices/i4/payments': [] })
      const { user } = renderApp(<PaymentsPage />)
      await pickStudent(user)
      await screen.findByText('$2400 · due 2030-02-01')

      await user.click(screen.getAllByLabelText('Expand')[3])

      expect(await screen.findByText('No payments recorded yet.')).toBeInTheDocument()
      expect(screen.queryByRole('button', { name: 'Record payment' })).not.toBeInTheDocument()
    })
  })

  describe('creating an invoice', () => {
    const packages = [{ id: 'pk1', courseId: 'c1', sessionCount: 12, price: 2400 }]

    it('creates an invoice from a package', async () => {
      const api = mockApi({ ...routes, 'GET /courses/c1/packages': packages, 'POST /students/s1/invoices': pending })
      const { user } = renderApp(<PaymentsPage />)
      await pickStudent(user)
      await screen.findByText('$2400 · due 2030-02-01')

      await user.click(screen.getByRole('button', { name: /New invoice/ }))
      await user.selectOptions(await screen.findByLabelText('Course'), 'c1')
      await user.selectOptions(await screen.findByLabelText('Package'), 'pk1')
      await user.type(screen.getByLabelText('Due date'), '2030-03-01')
      await user.click(screen.getByRole('button', { name: 'Create invoice' }))

      await waitFor(() => expect(api.made('POST', 'invoices')).toHaveLength(1))
      expect(api.made('POST', 'invoices')[0].body).toEqual({ packageId: 'pk1', amount: null, dueDate: '2030-03-01' })
    })

    it('creates an ad-hoc invoice for a fixed amount', async () => {
      const api = mockApi({ ...routes, 'POST /students/s1/invoices': pending })
      const { user } = renderApp(<PaymentsPage />)
      await pickStudent(user)
      await screen.findByText('$2400 · due 2030-02-01')

      await user.click(screen.getByRole('button', { name: /New invoice/ }))
      await user.click(screen.getByLabelText('Ad-hoc amount'))
      await user.type(screen.getByLabelText('Amount'), '200')
      await user.type(screen.getByLabelText('Due date'), '2030-03-01')
      await user.click(screen.getByRole('button', { name: 'Create invoice' }))

      await waitFor(() => expect(api.made('POST', 'invoices')).toHaveLength(1))
      expect(api.made('POST', 'invoices')[0].body).toEqual({ packageId: null, amount: 200, dueDate: '2030-03-01' })
    })

    it("shows the server's reason when an invoice cannot be created", async () => {
      mockApi({ ...routes, 'POST /students/s1/invoices': respond(400, { title: 'Bad request', errors: ['Amount is required for an ad-hoc invoice (no package).'] }) })
      const { user } = renderApp(<PaymentsPage />)
      await pickStudent(user)
      await screen.findByText('$2400 · due 2030-02-01')

      await user.click(screen.getByRole('button', { name: /New invoice/ }))
      await user.click(screen.getByLabelText('Ad-hoc amount'))
      await user.type(screen.getByLabelText('Due date'), '2030-03-01')
      await user.click(screen.getByRole('button', { name: 'Create invoice' }))

      expect(await screen.findByText('Amount is required for an ad-hoc invoice (no package).')).toBeInTheDocument()
    })
  })
})

describe('packages', () => {
  const pkg = { id: 'pk1', courseId: 'c1', sessionCount: 12, price: 2400 }
  const openTab = async (options: Parameters<typeof renderApp>[1] = {}) => {
    const view = renderApp(<PaymentsPage />, options)
    await view.user.click(await screen.findByRole('button', { name: 'Packages' }))
    await screen.findByRole('option', { name: /Python Fundamentals/ })
    await view.user.selectOptions(screen.getByRole('combobox'), 'c1')
    return view
  }

  it('lists a course’s packages, and asks for a course first', async () => {
    mockApi({ ...routes, 'GET /courses/c1/packages': [pkg] })
    const view = renderApp(<PaymentsPage />)
    await view.user.click(await screen.findByRole('button', { name: 'Packages' }))
    expect(screen.getByText('Choose a course to see its packages.')).toBeInTheDocument()

    await screen.findByRole('option', { name: /Python Fundamentals/ })
    await view.user.selectOptions(screen.getByRole('combobox'), 'c1')

    expect(await screen.findByText(/12 sessions/)).toBeInTheDocument()
    expect(screen.getByText('$2400')).toBeInTheDocument()
  })

  it('shows an empty state', async () => {
    mockApi({ ...routes, 'GET /courses/c1/packages': [] })
    await openTab()

    expect(await screen.findByText('No packages yet.')).toBeInTheDocument()
  })

  it('creates a package, validating positive values', async () => {
    const api = mockApi({ ...routes, 'GET /courses/c1/packages': [], 'POST /courses/c1/packages': pkg })
    const { user } = await openTab()
    await screen.findByText('No packages yet.')

    await user.click(screen.getByRole('button', { name: /New package/ }))
    await user.clear(screen.getByLabelText('Session count'))
    await user.type(screen.getByLabelText('Session count'), '0')
    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Session count must be greater than 0')).toBeInTheDocument()
    expect(screen.getByText('Price must be greater than 0')).toBeInTheDocument()
    expect(api.made('POST')).toHaveLength(0)

    await user.clear(screen.getByLabelText('Session count'))
    await user.type(screen.getByLabelText('Session count'), '12')
    await user.clear(screen.getByLabelText('Price'))
    await user.type(screen.getByLabelText('Price'), '2400')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.made('POST')).toHaveLength(1))
    expect(api.made('POST')[0].body).toEqual({ sessionCount: 12, price: 2400 })
  })

  it('edits a package, and shows why a save failed', async () => {
    const api = mockApi({ ...routes, 'GET /courses/c1/packages': [pkg], 'PUT /packages/pk1': pkg })
    const { user } = await openTab()

    await user.click(await screen.findByLabelText('Edit package'))
    expect(screen.getByLabelText('Price')).toHaveValue(2400)
    await user.clear(screen.getByLabelText('Price'))
    await user.type(screen.getByLabelText('Price'), '3000')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.made('PUT')).toHaveLength(1))
    expect(api.made('PUT')[0].body).toEqual({ sessionCount: 12, price: 3000 })
  })

  it('reports a package that cannot be saved', async () => {
    mockApi({ ...routes, 'GET /courses/c1/packages': [pkg], 'PUT /packages/pk1': respond(403, { title: 'You do not have access to this branch.' }) })
    const { user } = await openTab()

    await user.click(await screen.findByLabelText('Edit package'))
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('You do not have access to this branch.')).toBeInTheDocument()
  })

  it('deletes a package after confirmation, and explains a refusal', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const alert = vi.spyOn(window, 'alert').mockImplementation(() => {})
    mockApi({
      ...routes,
      'GET /courses/c1/packages': [pkg],
      'DELETE /packages/pk1': respond(400, { title: 'Bad request', errors: ['Cannot delete a package that has invoices issued against it.'] }),
    })
    const { user } = await openTab()

    await user.click(await screen.findByLabelText('Delete package'))

    await waitFor(() => expect(alert).toHaveBeenCalledWith('Cannot delete a package that has invoices issued against it.'))
  })
})

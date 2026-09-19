import { screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mockApi, renderApp, respond } from '@/test/harness'
import { PayrollPage } from './PayrollPage'

const teacher = { id: 't1', userId: 'u-t1', fullName: 'Mona Adel', email: 'mona@example.com', hireDate: '2025-01-01', isActive: true, branchIds: ['b1'] }
const teacherRun = (id: string, status: string, total = 1500) => ({
  id, teacherId: 't1', periodStart: '2030-01-01', periodEnd: '2030-01-31', totalAmount: total, status,
})
const staffRun = (id: string, status: string, amount = 8000) => ({
  id, userId: 'u-fd', periodStart: '2030-01-01', periodEnd: '2030-01-31', amount, status,
})
const staff = [
  { userId: 'u-fd', fullName: 'Nour Samir', email: 'n@example.com', roles: ['FrontDesk'], branchIds: ['b1'], isActive: true },
  { userId: 'u-bm', fullName: 'Hoda Fathy', email: 'h@example.com', roles: ['BranchManager'], branchIds: ['b1'], isActive: true },
  { userId: 'u-t', fullName: 'Mona Adel', email: 'mona@example.com', roles: ['Teacher'], branchIds: [], isActive: true },
]

const createObjectURL = vi.fn(() => 'blob:paystub')
const revokeObjectURL = vi.fn()

beforeEach(() => {
  Object.assign(URL, { createObjectURL, revokeObjectURL })
})
afterEach(() => {
  vi.restoreAllMocks()
  createObjectURL.mockClear()
  revokeObjectURL.mockClear()
})

describe('PayrollPage routing by role', () => {
  it("shows a teacher only their own runs, with no management controls", async () => {
    mockApi({ 'GET /teachers/my-payroll-runs': [teacherRun('r1', 'Draft'), teacherRun('r2', 'Approved')] })
    renderApp(<PayrollPage />, { roles: ['Teacher'] })

    expect(await screen.findByText('Your payroll runs.')).toBeInTheDocument()
    expect(await screen.findByText('Draft')).toBeInTheDocument()
    expect(screen.getByText('Approved')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Mark paid' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Teachers' })).not.toBeInTheDocument()
  })

  it('shows front desk and branch managers their own staff pay, read-only', async () => {
    mockApi({ 'GET /users/my-staff-payroll-runs': [staffRun('sr1', 'Draft'), staffRun('sr2', 'Paid', 7500)] })
    renderApp(<PayrollPage />, { roles: ['FrontDesk'] })

    expect(await screen.findByText('$8000')).toBeInTheDocument()
    expect(screen.getByText('Paid')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Edit amount')).not.toBeInTheDocument()
  })

  it('reports a failure to load own payroll for a teacher and for staff', async () => {
    mockApi({ 'GET /teachers/my-payroll-runs': respond(500) })
    const first = renderApp(<PayrollPage />, { roles: ['Teacher'] })
    expect(await screen.findByText('Could not load your payroll runs.')).toBeInTheDocument()
    first.unmount()

    mockApi({ 'GET /users/my-staff-payroll-runs': respond(500) })
    renderApp(<PayrollPage />, { roles: ['BranchManager'] })
    expect(await screen.findByText('Could not load your payroll runs.')).toBeInTheDocument()
  })

  it('says when there are no runs yet', async () => {
    mockApi({ 'GET /teachers/my-payroll-runs': [] })
    renderApp(<PayrollPage />, { roles: ['Teacher'] })

    expect(await screen.findByText('No payroll runs yet.')).toBeInTheDocument()
  })

  it('switches between the teacher and staff tabs and back again', async () => {
    mockApi({ 'GET /teachers': [teacher], 'GET /users/staff': staff })
    const { user } = renderApp(<PayrollPage />, { roles: ['Owner'] })
    expect(await screen.findByText('Choose a teacher to see their payroll runs.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Front Desk & Branch Managers' }))
    expect(screen.getByText('Choose a Front Desk or Branch Manager staff member.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Teachers' }))
    expect(screen.getByText('Choose a teacher to see their payroll runs.')).toBeInTheDocument()
  })

  it('gives the owner the management tabs', async () => {
    mockApi({ 'GET /teachers': [teacher], 'GET /users/staff': staff })
    renderApp(<PayrollPage />, { roles: ['Owner'] })

    expect(await screen.findByText('Generate, approve, and pay payroll runs.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Teachers' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Front Desk & Branch Managers' })).toBeInTheDocument()
  })
})

describe('teacher payroll management (Owner)', () => {
  const routes = {
    'GET /teachers': [teacher],
    'GET /users/staff': staff,
    'GET /teachers/t1/payroll-runs': [teacherRun('r1', 'Draft'), teacherRun('r2', 'Approved'), teacherRun('r3', 'Paid')],
  }

  async function chooseTeacher(user: ReturnType<typeof renderApp>['user']) {
    await screen.findByRole('option', { name: 'Mona Adel' })
    await user.selectOptions(screen.getByRole('combobox'), 't1')
  }

  it('asks for a teacher first, then lists their runs', async () => {
    mockApi(routes)
    const { user } = renderApp(<PayrollPage />, { roles: ['Owner'] })
    expect(await screen.findByText('Choose a teacher to see their payroll runs.')).toBeInTheDocument()

    await chooseTeacher(user)

    expect(await screen.findByText('Draft')).toBeInTheDocument()
    expect(screen.getByText('Approved')).toBeInTheDocument()
    expect(screen.getByText('Paid')).toBeInTheDocument()
    expect(screen.getAllByText('$1500')).toHaveLength(3)
  })

  it('offers Approve only for drafts and Mark paid only for approved runs', async () => {
    mockApi(routes)
    const { user } = renderApp(<PayrollPage />, { roles: ['Owner'] })
    await chooseTeacher(user)
    await screen.findByText('Draft')

    expect(screen.getAllByRole('button', { name: 'Approve' })).toHaveLength(1)
    expect(screen.getAllByRole('button', { name: 'Mark paid' })).toHaveLength(1)
  })

  it('approves and marks paid, and reports a refusal', async () => {
    const alert = vi.spyOn(window, 'alert').mockImplementation(() => {})
    const api = mockApi({
      ...routes,
      'POST /payroll-runs/r1/approve': teacherRun('r1', 'Approved'),
      'POST /payroll-runs/r2/mark-paid': respond(400, { title: 'Bad request', errors: ['Only an approved run can be marked paid.'] }),
    })
    const { user } = renderApp(<PayrollPage />, { roles: ['Owner'] })
    await chooseTeacher(user)
    await screen.findByText('Draft')

    await user.click(screen.getByRole('button', { name: 'Approve' }))
    await waitFor(() => expect(api.made('POST', '/payroll-runs/r1/approve')).toHaveLength(1))

    await user.click(screen.getByRole('button', { name: 'Mark paid' }))
    await waitFor(() => expect(alert).toHaveBeenCalledWith('Only an approved run can be marked paid.'))
  })

  it('reports a failed approval', async () => {
    const alert = vi.spyOn(window, 'alert').mockImplementation(() => {})
    mockApi({ ...routes, 'POST /payroll-runs/r1/approve': respond(500) })
    const { user } = renderApp(<PayrollPage />, { roles: ['Owner'] })
    await chooseTeacher(user)
    await screen.findByText('Draft')

    await user.click(screen.getByRole('button', { name: 'Approve' }))

    await waitFor(() => expect(alert).toHaveBeenCalledWith('Could not approve this run.'))
  })

  it('shows the per-session breakdown of a run when expanded', async () => {
    mockApi({
      ...routes,
      'GET /payroll-runs/r1/line-items': [
        { id: 'li1', payrollRunId: 'r1', courseSessionId: 'cs1', amount: 750 },
        { id: 'li2', payrollRunId: 'r1', courseSessionId: 'cs2', amount: 750 },
      ],
      'GET /sessions/cs1': { id: 'cs1', startUtc: '2030-01-05T09:00:00Z' },
      'GET /sessions/cs2': { id: 'cs2', startUtc: '2030-01-12T09:00:00Z' },
    })
    const { user } = renderApp(<PayrollPage />, { roles: ['Owner'] })
    await chooseTeacher(user)
    await screen.findByText('Draft')

    await user.click(screen.getAllByLabelText('Expand')[0])

    expect(await screen.findByText('Paid sessions (2)')).toBeInTheDocument()
    expect(await screen.findByText('2030-01-05 09:00')).toBeInTheDocument()
    expect(screen.getByText('2030-01-12 09:00')).toBeInTheDocument()
    expect(screen.getAllByText('$750')).toHaveLength(2)

    await user.click(screen.getByLabelText('Collapse'))
    expect(screen.queryByText('Paid sessions (2)')).not.toBeInTheDocument()
  })

  it('explains an empty breakdown', async () => {
    mockApi({ ...routes, 'GET /payroll-runs/r1/line-items': [] })
    const { user } = renderApp(<PayrollPage />, { roles: ['Owner'] })
    await chooseTeacher(user)
    await screen.findByText('Draft')

    await user.click(screen.getAllByLabelText('Expand')[0])

    expect(await screen.findByText('No per-session breakdown to show for this run.')).toBeInTheDocument()
  })

  it('downloads the pay stub, and reports when it cannot', async () => {
    const alert = vi.spyOn(window, 'alert').mockImplementation(() => {})
    const api = mockApi({ ...routes, 'GET /payroll-runs/r1/paystub': {}, 'GET /payroll-runs/r2/paystub': respond(500) })
    const { user } = renderApp(<PayrollPage />, { roles: ['Owner'] })
    await chooseTeacher(user)
    await screen.findByText('Draft')

    const stubs = screen.getAllByRole('button', { name: 'Pay stub' })
    await user.click(stubs[0])
    await waitFor(() => expect(createObjectURL).toHaveBeenCalledTimes(1))
    expect(api.made('GET', 'r1/paystub')).toHaveLength(1)
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:paystub')

    await user.click(stubs[1])
    await waitFor(() => expect(alert).toHaveBeenCalledWith('Could not download this pay stub.'))
  })

  it('generates a run for the chosen period', async () => {
    const api = mockApi({ ...routes, 'POST /teachers/t1/payroll-runs': teacherRun('r9', 'Draft') })
    const { user } = renderApp(<PayrollPage />, { roles: ['Owner'] })
    await chooseTeacher(user)
    await screen.findByText('Draft')

    await user.click(screen.getByRole('button', { name: /Generate run/ }))
    await user.type(screen.getByLabelText('Period start'), '2030-02-01')
    await user.type(screen.getByLabelText('Period end'), '2030-02-28')
    await user.click(screen.getByRole('button', { name: 'Generate' }))

    await waitFor(() => expect(api.made('POST', '/payroll-runs')).toHaveLength(1))
    expect(api.made('POST', '/payroll-runs')[0].body).toEqual({ periodStart: '2030-02-01', periodEnd: '2030-02-28' })
    await waitFor(() => expect(screen.queryByText('Generate payroll run')).not.toBeInTheDocument())
  })

  it('shows why a run could not be generated, and can be cancelled', async () => {
    mockApi({ ...routes, 'POST /teachers/t1/payroll-runs': respond(400, { title: 'Bad request', errors: ['No completed sessions in this period.'] }) })
    const { user } = renderApp(<PayrollPage />, { roles: ['Owner'] })
    await chooseTeacher(user)
    await screen.findByText('Draft')

    await user.click(screen.getByRole('button', { name: /Generate run/ }))
    await user.type(screen.getByLabelText('Period start'), '2030-02-01')
    await user.type(screen.getByLabelText('Period end'), '2030-02-28')
    await user.click(screen.getByRole('button', { name: 'Generate' }))

    expect(await screen.findByText('No completed sessions in this period.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Cancel' }))
    expect(screen.queryByText('Generate payroll run')).not.toBeInTheDocument()
  })
})

describe('staff payroll management (Owner)', () => {
  const routes = {
    'GET /teachers': [teacher],
    'GET /users/staff': staff,
    'GET /users/staff/u-fd/payroll-runs': [staffRun('sr1', 'Draft'), staffRun('sr2', 'Approved', 9000), staffRun('sr3', 'Paid', 7000)],
  }

  async function openStaffTab() {
    const view = renderApp(<PayrollPage />, { roles: ['Owner'] })
    await view.user.click(await screen.findByRole('button', { name: 'Front Desk & Branch Managers' }))
    await screen.findByRole('option', { name: /Nour Samir/ })
    return view
  }

  it('lists only front desk and branch managers, never teachers', async () => {
    mockApi(routes)
    await openStaffTab()

    expect(screen.getByText('Choose a Front Desk or Branch Manager staff member.')).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Nour Samir (FrontDesk)' })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Hoda Fathy (BranchManager)' })).toBeInTheDocument()
    expect(screen.queryByRole('option', { name: /Mona Adel/ })).not.toBeInTheDocument()
  })

  it('lists a staff member’s runs with the right actions for each status', async () => {
    mockApi(routes)
    const { user } = await openStaffTab()

    await user.selectOptions(screen.getByRole('combobox'), 'u-fd')

    expect(await screen.findByText('$8000')).toBeInTheDocument()
    expect(screen.getByText('$9000')).toBeInTheDocument()
    expect(screen.getByText('$7000')).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: 'Approve' })).toHaveLength(1)
    expect(screen.getAllByRole('button', { name: 'Mark paid' })).toHaveLength(1)
    expect(screen.getAllByLabelText('Edit amount')).toHaveLength(1)   // only the draft is editable
  })

  it('approves, marks paid, and reports refusals', async () => {
    const alert = vi.spyOn(window, 'alert').mockImplementation(() => {})
    const api = mockApi({
      ...routes,
      'POST /staff-payroll-runs/sr1/approve': staffRun('sr1', 'Approved'),
      'POST /staff-payroll-runs/sr2/mark-paid': respond(400, { title: 'Bad request', errors: ['Run is already paid.'] }),
    })
    const { user } = await openStaffTab()
    await user.selectOptions(screen.getByRole('combobox'), 'u-fd')
    await screen.findByText('$8000')

    await user.click(screen.getByRole('button', { name: 'Approve' }))
    await waitFor(() => expect(api.made('POST', 'sr1/approve')).toHaveLength(1))

    await user.click(screen.getByRole('button', { name: 'Mark paid' }))
    await waitFor(() => expect(alert).toHaveBeenCalledWith('Run is already paid.'))
  })

  it('reports a failed approval with a fallback message', async () => {
    const alert = vi.spyOn(window, 'alert').mockImplementation(() => {})
    mockApi({ ...routes, 'POST /staff-payroll-runs/sr1/approve': respond(500) })
    const { user } = await openStaffTab()
    await user.selectOptions(screen.getByRole('combobox'), 'u-fd')
    await screen.findByText('$8000')

    await user.click(screen.getByRole('button', { name: 'Approve' }))

    await waitFor(() => expect(alert).toHaveBeenCalledWith('Could not approve this run.'))
  })

  it('edits a draft amount', async () => {
    const api = mockApi({ ...routes, 'PUT /staff-payroll-runs/sr1': staffRun('sr1', 'Draft', 8500) })
    const { user } = await openStaffTab()
    await user.selectOptions(screen.getByRole('combobox'), 'u-fd')
    await screen.findByText('$8000')

    await user.click(screen.getByLabelText('Edit amount'))
    const amount = screen.getByDisplayValue('8000')
    await user.clear(amount)
    await user.type(amount, '8500')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.made('PUT')).toHaveLength(1))
    expect(api.made('PUT')[0].body).toEqual({ amount: 8500 })
    await waitFor(() => expect(screen.queryByDisplayValue('8500')).not.toBeInTheDocument())
  })

  it('will not save a zero amount, can be cancelled, and reports a refusal', async () => {
    const alert = vi.spyOn(window, 'alert').mockImplementation(() => {})
    const api = mockApi({ ...routes, 'PUT /staff-payroll-runs/sr1': respond(400, { title: 'Bad request', errors: ['Only draft runs can be edited.'] }) })
    const { user } = await openStaffTab()
    await user.selectOptions(screen.getByRole('combobox'), 'u-fd')
    await screen.findByText('$8000')

    await user.click(screen.getByLabelText('Edit amount'))
    const amount = screen.getByDisplayValue('8000')
    await user.clear(amount)
    await user.type(amount, '0')
    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(api.made('PUT')).toHaveLength(0)

    await user.clear(amount)
    await user.type(amount, '5')
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(alert).toHaveBeenCalledWith('Only draft runs can be edited.'))
    expect(screen.getByDisplayValue('5')).toBeInTheDocument()   // still editing after a refusal

    await user.click(screen.getAllByRole('button', { name: 'Cancel' })[0])
    expect(screen.queryByDisplayValue('5')).not.toBeInTheDocument()
    expect(screen.getByText('$8000')).toBeInTheDocument()
  })

  it('downloads a staff pay stub, and reports when it cannot', async () => {
    const alert = vi.spyOn(window, 'alert').mockImplementation(() => {})
    const api = mockApi({ ...routes, 'GET /staff-payroll-runs/sr1/paystub': {}, 'GET /staff-payroll-runs/sr2/paystub': respond(500) })
    const { user } = await openStaffTab()
    await user.selectOptions(screen.getByRole('combobox'), 'u-fd')
    await screen.findByText('$8000')

    const stubs = screen.getAllByRole('button', { name: 'Pay stub' })
    await user.click(stubs[0])
    await waitFor(() => expect(createObjectURL).toHaveBeenCalledTimes(1))
    expect(api.made('GET', 'sr1/paystub')).toHaveLength(1)

    await user.click(stubs[1])
    await waitFor(() => expect(alert).toHaveBeenCalledWith('Could not download this pay stub.'))
  })

  it('generates a run with a period and amount', async () => {
    const api = mockApi({ ...routes, 'POST /users/staff/u-fd/payroll-runs': staffRun('sr9', 'Draft') })
    const { user } = await openStaffTab()
    await user.selectOptions(screen.getByRole('combobox'), 'u-fd')
    await screen.findByText('$8000')

    await user.click(screen.getByRole('button', { name: /Generate run/ }))
    await user.type(screen.getByLabelText('Period start'), '2030-02-01')
    await user.type(screen.getByLabelText('Period end'), '2030-02-28')
    await user.type(screen.getByLabelText('Amount'), '8200')
    await user.click(screen.getByRole('button', { name: 'Generate' }))

    await waitFor(() => expect(api.made('POST', 'payroll-runs')).toHaveLength(1))
    expect(api.made('POST', 'payroll-runs')[0].body).toEqual({ periodStart: '2030-02-01', periodEnd: '2030-02-28', amount: 8200 })
  })

  it('shows why a staff run could not be generated', async () => {
    mockApi({ ...routes, 'POST /users/staff/u-fd/payroll-runs': respond(400, { title: 'Bad request', errors: ['A run already covers this period.'] }) })
    const { user } = await openStaffTab()
    await user.selectOptions(screen.getByRole('combobox'), 'u-fd')
    await screen.findByText('$8000')

    await user.click(screen.getByRole('button', { name: /Generate run/ }))
    await user.type(screen.getByLabelText('Period start'), '2030-01-01')
    await user.type(screen.getByLabelText('Period end'), '2030-01-31')
    await user.type(screen.getByLabelText('Amount'), '8000')
    await user.click(screen.getByRole('button', { name: 'Generate' }))

    expect(await screen.findByText('A run already covers this period.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Cancel' }))
    expect(screen.queryByText('Generate payroll run')).not.toBeInTheDocument()
  })
})

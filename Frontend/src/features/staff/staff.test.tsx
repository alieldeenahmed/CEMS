import { screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { mockApi, renderApp, respond } from '@/test/harness'
import { StaffPage, branchLabel } from './StaffPage'

const SMOUHA = { id: 'b-smouha', name: 'CodeCamp Smouha', address: '', phone: '', isActive: true }
const KAFR = { id: 'b-kafr', name: 'CodeCamp Kafr Abdo', address: '', phone: '', isActive: true }

const owner = { userId: 'u0', email: 'owner@codecamp.demo', fullName: 'Mostafa El-Sayed', roles: ['Owner'], branchIds: [], isActive: true }
const manager = { userId: 'u1', email: 'bm.smouha@codecamp.demo', fullName: 'Nourhan Adel', roles: ['BranchManager'], branchIds: [SMOUHA.id], isActive: true }
const teacher = { userId: 'u2', email: 'teacher@codecamp.demo', fullName: 'Ahmed Nabil', roles: ['Teacher'], branchIds: [], isActive: true }
const frontDesk = { userId: 'u3', email: 'fd@codecamp.demo', fullName: 'Mariam Younis', roles: ['FrontDesk'], branchIds: [SMOUHA.id], isActive: false }

const ownerRoutes = { 'GET /users/staff': [owner, manager, teacher, frontDesk], 'GET /branches': [SMOUHA, KAFR] }

describe('branchLabel', () => {
  const names = new Map([[SMOUHA.id, SMOUHA.name]])

  it('names the branches of a branch-bound account', () => {
    expect(branchLabel(manager, names)).toBe('CodeCamp Smouha')
  })

  it('says "All branches" only for an Owner', () => {
    expect(branchLabel(owner, names)).toBe('All branches')
  })

  it("does not call a teacher 'All branches': their branches are set on their teacher profile", () => {
    expect(branchLabel(teacher, names)).toBe('Set on teacher profile')
  })

  it('flags a branch-bound account that has no branch, and an unknown branch id', () => {
    expect(branchLabel({ ...frontDesk, branchIds: [] }, names)).toBe('No branch assigned')
    expect(branchLabel({ ...manager, branchIds: ['gone'] }, names)).toBe('Unknown')
  })
})

describe('StaffPage as the Owner', () => {
  it('lists every account with its roles, branches and status', async () => {
    mockApi(ownerRoutes)
    renderApp(<StaffPage />)

    expect(await screen.findByText('Mostafa El-Sayed')).toBeInTheDocument()
    expect(screen.getByText('Branch Manager')).toBeInTheDocument()
    expect(screen.getByText('Front Desk')).toBeInTheDocument()
    expect(screen.getByText('Inactive')).toBeInTheDocument()
    expect(screen.getByText('All branches')).toBeInTheDocument()
    expect(screen.getByText('Set on teacher profile')).toBeInTheDocument()
  })

  it('searches by name or email, and reports no matches', async () => {
    mockApi(ownerRoutes)
    const { user } = renderApp(<StaffPage />)
    await screen.findByText('Mostafa El-Sayed')

    await user.type(screen.getByLabelText('Search staff'), 'fd@')
    expect(screen.queryByText('Nourhan Adel')).not.toBeInTheDocument()
    expect(screen.getByText('Mariam Younis')).toBeInTheDocument()

    await user.clear(screen.getByLabelText('Search staff'))
    await user.type(screen.getByLabelText('Search staff'), 'nobody')
    expect(screen.getByText('No staff accounts match your search.')).toBeInTheDocument()
  })

  it('deactivates an active account and reactivates an inactive one', async () => {
    const api = mockApi({ ...ownerRoutes, 'PUT /users/staff/u1/status': respond(204), 'PUT /users/staff/u3/status': respond(204) })
    const { user } = renderApp(<StaffPage />)
    await screen.findByText('Nourhan Adel')

    await user.click(screen.getAllByRole('button', { name: 'Deactivate' })[1])
    await user.click(screen.getByRole('button', { name: 'Activate' }))

    await waitFor(() => expect(api.made('PUT')).toHaveLength(2))
    expect(api.made('PUT').map((c) => c.body)).toEqual(expect.arrayContaining([{ isActive: false }, { isActive: true }]))
  })

  it('shows an error when the accounts cannot be loaded', async () => {
    mockApi({ ...ownerRoutes, 'GET /users/staff': respond(500) })
    renderApp(<StaffPage />)

    expect(await screen.findByText('Could not load staff accounts.')).toBeInTheDocument()
  })

  it('creates a branch manager, who must be given a branch', async () => {
    const api = mockApi({ ...ownerRoutes, 'POST /users/staff': manager })
    const { user } = renderApp(<StaffPage />)
    await screen.findByText('Nourhan Adel')

    await user.click(screen.getByRole('button', { name: /New staff account/ }))
    await user.type(screen.getByLabelText('Full name'), 'Hossam Fathy')
    await user.type(screen.getByLabelText('Email'), 'bm.kafrabdo@codecamp.demo')
    await user.type(screen.getByLabelText('Password'), 'DemoPass123')
    await user.type(screen.getByLabelText('Phone number'), '01012340003')
    await user.click(screen.getByRole('button', { name: 'Create account' }))
    expect(await screen.findByText('Branch is required for this role')).toBeInTheDocument()
    expect(api.made('POST')).toHaveLength(0)

    await user.selectOptions(screen.getByLabelText('Branch'), KAFR.id)
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    await waitFor(() => expect(api.made('POST')).toHaveLength(1))
    expect(api.made('POST')[0].body).toEqual({
      email: 'bm.kafrabdo@codecamp.demo', password: 'DemoPass123', fullName: 'Hossam Fathy',
      phoneNumber: '01012340003', role: 'BranchManager', branchId: KAFR.id,
    })
  })

  it('creates a teacher without asking for a branch', async () => {
    const api = mockApi({ ...ownerRoutes, 'POST /users/staff': teacher })
    const { user } = renderApp(<StaffPage />)
    await screen.findByText('Nourhan Adel')

    await user.click(screen.getByRole('button', { name: /New staff account/ }))
    await user.selectOptions(screen.getByLabelText('Role'), 'Teacher')
    expect(screen.queryByLabelText('Branch')).not.toBeInTheDocument()
    await user.type(screen.getByLabelText('Full name'), 'Sara Ibrahim')
    await user.type(screen.getByLabelText('Email'), 'sara@codecamp.demo')
    await user.type(screen.getByLabelText('Password'), 'DemoPass123')
    await user.type(screen.getByLabelText('Phone number'), '0100')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    await waitFor(() => expect(api.made('POST')).toHaveLength(1))
    expect(api.made('POST')[0].body).toMatchObject({ role: 'Teacher', branchId: null })
  })

  it('validates the form: a short password and a missing name are rejected before any request', async () => {
    const api = mockApi(ownerRoutes)
    const { user } = renderApp(<StaffPage />)
    await screen.findByText('Nourhan Adel')

    await user.click(screen.getByRole('button', { name: /New staff account/ }))
    await user.type(screen.getByLabelText('Password'), 'short')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findByText('Password must be at least 8 characters')).toBeInTheDocument()
    expect(screen.getByText('Full name is required')).toBeInTheDocument()
    expect(api.made('POST')).toHaveLength(0)
  })

  it('shows a message and keeps the dialog open if the account cannot be created', async () => {
    mockApi({ ...ownerRoutes, 'POST /users/staff': respond(400, { title: 'Bad request', errors: ['Email is already taken.'] }) })
    const { user } = renderApp(<StaffPage />)
    await screen.findByText('Nourhan Adel')

    await user.click(screen.getByRole('button', { name: /New staff account/ }))
    await user.selectOptions(screen.getByLabelText('Role'), 'Teacher')
    await user.type(screen.getByLabelText('Full name'), 'Sara')
    await user.type(screen.getByLabelText('Email'), 'sara@codecamp.demo')
    await user.type(screen.getByLabelText('Password'), 'DemoPass123')
    await user.type(screen.getByLabelText('Phone number'), '0100')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findByText(/The email may already be in use/)).toBeInTheDocument()
  })

  it('resets a password and tells the user to relay it, since no email is sent', async () => {
    const api = mockApi({ ...ownerRoutes, 'POST /users/staff/u2/reset-password': respond(204) })
    const { user } = renderApp(<StaffPage />)

    await user.click(await screen.findByLabelText('Reset password for Ahmed Nabil'))
    await user.type(screen.getByLabelText('New password'), 'BrandNewPass1')
    await user.click(screen.getByRole('button', { name: 'Reset password' }))

    expect(await screen.findByText(/Share the new password with Ahmed Nabil directly/)).toBeInTheDocument()
    expect(api.made('POST')[0].body).toEqual({ newPassword: 'BrandNewPass1' })
    await user.click(screen.getByRole('button', { name: 'Done' }))
    expect(screen.queryByText(/Share the new password/)).not.toBeInTheDocument()
  })

  it('rejects a short new password, and reports a refusal from the server', async () => {
    mockApi({ ...ownerRoutes, 'POST /users/staff/u2/reset-password': respond(403) })
    const { user } = renderApp(<StaffPage />)

    await user.click(await screen.findByLabelText('Reset password for Ahmed Nabil'))
    await user.type(screen.getByLabelText('New password'), 'short')
    await user.click(screen.getByRole('button', { name: 'Reset password' }))
    expect(await screen.findByText('Password must be at least 8 characters')).toBeInTheDocument()

    await user.clear(screen.getByLabelText('New password'))
    await user.type(screen.getByLabelText('New password'), 'LongEnough123')
    await user.click(screen.getByRole('button', { name: 'Reset password' }))
    expect(await screen.findByText(/You may not have access to reset this account/)).toBeInTheDocument()
  })
})

describe('StaffPage as a Branch Manager', () => {
  const managerRoutes = { 'GET /users/staff/my-branch': [teacher, frontDesk], 'GET /branches': [SMOUHA] }

  it("loads only their branch's roster, and cannot deactivate accounts", async () => {
    const api = mockApi(managerRoutes)
    renderApp(<StaffPage />, { roles: ['BranchManager'], branchIds: [SMOUHA.id] })

    expect(await screen.findByText('Ahmed Nabil')).toBeInTheDocument()
    expect(screen.getByText('Front desk and teacher accounts at your branch.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Deactivate' })).not.toBeInTheDocument()
    expect(api.made('GET', '/users/staff')).toHaveLength(1)
    expect(api.made('GET', '/users/staff')[0].path).toBe('/users/staff/my-branch')
  })

  it('can only create front desk and teacher accounts, fixed to their own branch', async () => {
    const api = mockApi({ ...managerRoutes, 'POST /users/staff': frontDesk })
    const { user } = renderApp(<StaffPage />, { roles: ['BranchManager'], branchIds: [SMOUHA.id] })
    await screen.findByText('Ahmed Nabil')

    await user.click(screen.getByRole('button', { name: /New staff account/ }))
    const roleOptions = screen.getAllByRole('option').map((o) => o.textContent)
    expect(roleOptions).toEqual(['Front Desk', 'Teacher'])
    expect(screen.queryByLabelText('Branch')).not.toBeInTheDocument()

    await user.type(screen.getByLabelText('Full name'), 'Youssef Ali')
    await user.type(screen.getByLabelText('Email'), 'youssef@codecamp.demo')
    await user.type(screen.getByLabelText('Password'), 'DemoPass123')
    await user.type(screen.getByLabelText('Phone number'), '0100')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    await waitFor(() => expect(api.made('POST')).toHaveLength(1))
    expect(api.made('POST')[0].body).toMatchObject({ role: 'FrontDesk', branchId: SMOUHA.id })
  })
})

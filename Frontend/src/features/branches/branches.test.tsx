import { screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mockApi, renderApp, respond } from '@/test/harness'
import { BranchesPage } from './BranchesPage'

const smouha = { id: 'b1', name: 'CodeCamp Smouha', address: '14 Fawzy Moaz St', phone: '034567001', isActive: true }
const kafr = { id: 'b2', name: 'CodeCamp Kafr Abdo', address: '9 Abdel Salam Aref St', phone: '034567002', isActive: false }

const routes = {
  'GET /branches': [smouha, kafr],
  'GET /branches/b1/rooms': [
    { id: 'r1', branchId: 'b1', name: 'Lab 1', capacity: 15 },
    { id: 'r2', branchId: 'b1', name: 'Lab 2 (1:1)', capacity: 1 },
  ],
}

afterEach(() => vi.restoreAllMocks())

describe('BranchesPage', () => {
  it('lists branches with address, phone and status', async () => {
    mockApi(routes)
    renderApp(<BranchesPage />)

    expect(await screen.findByText('CodeCamp Smouha')).toBeInTheDocument()
    expect(screen.getByText('14 Fawzy Moaz St · 034567001')).toBeInTheDocument()
    expect(screen.getByText('Active')).toBeInTheDocument()
    expect(screen.getByText('Inactive')).toBeInTheDocument()
  })

  it('shows an empty state and a load error', async () => {
    mockApi({ 'GET /branches': [] })
    const { unmount } = renderApp(<BranchesPage />)
    expect(await screen.findByText('No branches yet.')).toBeInTheDocument()
    unmount()

    mockApi({ 'GET /branches': respond(500) })
    renderApp(<BranchesPage />)
    expect(await screen.findByText('Could not load branches.')).toBeInTheDocument()
  })

  it('toggles a branch between active and inactive, keeping its other details', async () => {
    const api = mockApi({ ...routes, 'PUT /branches/b1': { ...smouha, isActive: false }, 'PUT /branches/b2': { ...kafr, isActive: true } })
    const { user } = renderApp(<BranchesPage />)
    await screen.findByText('CodeCamp Smouha')

    await user.click(screen.getByRole('button', { name: 'Deactivate' }))
    await user.click(screen.getByRole('button', { name: 'Activate' }))

    await waitFor(() => expect(api.made('PUT')).toHaveLength(2))
    expect(api.made('PUT')[0].body).toEqual({ name: 'CodeCamp Smouha', address: '14 Fawzy Moaz St', phone: '034567001', isActive: false })
    expect(api.made('PUT')[1].body).toMatchObject({ isActive: true })
  })

  it('creates a branch', async () => {
    const api = mockApi({ ...routes, 'POST /branches': { ...smouha, id: 'b3' } })
    const { user } = renderApp(<BranchesPage />)
    await screen.findByText('CodeCamp Smouha')

    await user.click(screen.getByRole('button', { name: /New branch/ }))
    await user.type(screen.getByLabelText('Name'), 'CodeCamp Sidi Gaber')
    await user.type(screen.getByLabelText('Address'), '5 Main St')
    await user.type(screen.getByLabelText('Phone'), '034567003')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.made('POST')).toHaveLength(1))
    expect(api.made('POST')[0].body).toEqual({ name: 'CodeCamp Sidi Gaber', address: '5 Main St', phone: '034567003' })
  })

  it('requires every field', async () => {
    const api = mockApi(routes)
    const { user } = renderApp(<BranchesPage />)
    await screen.findByText('CodeCamp Smouha')

    await user.click(screen.getByRole('button', { name: /New branch/ }))
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('Name is required')).toBeInTheDocument()
    expect(screen.getByText('Address is required')).toBeInTheDocument()
    expect(screen.getByText('Phone is required')).toBeInTheDocument()
    expect(api.made('POST')).toHaveLength(0)
  })

  it('edits a branch, prefilled with its current details', async () => {
    const api = mockApi({ ...routes, 'PUT /branches/b1': smouha })
    const { user } = renderApp(<BranchesPage />)

    await user.click(await screen.findByLabelText('Edit CodeCamp Smouha'))
    expect(screen.getByLabelText('Address')).toHaveValue('14 Fawzy Moaz St')
    await user.clear(screen.getByLabelText('Phone'))
    await user.type(screen.getByLabelText('Phone'), '0349999999')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.made('PUT')).toHaveLength(1))
    expect(api.made('PUT')[0].body).toEqual({ name: 'CodeCamp Smouha', address: '14 Fawzy Moaz St', phone: '0349999999', isActive: true })
  })

  it('shows the reason when a branch cannot be saved', async () => {
    mockApi({ ...routes, 'POST /branches': respond(400, { title: 'Validation failed', errors: { Phone: ["'Phone' must not be empty."] } }) })
    const { user } = renderApp(<BranchesPage />)
    await screen.findByText('CodeCamp Smouha')

    await user.click(screen.getByRole('button', { name: /New branch/ }))
    await user.type(screen.getByLabelText('Name'), 'X')
    await user.type(screen.getByLabelText('Address'), 'Y')
    await user.type(screen.getByLabelText('Phone'), 'Z')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText("'Phone' must not be empty.")).toBeInTheDocument()
  })
})

describe('rooms inside a branch', () => {
  const open = async () => {
    const view = renderApp(<BranchesPage />)
    await view.user.click((await screen.findAllByLabelText('Expand'))[0])
    await screen.findByText(/Lab 1/)
    return view
  }

  it('lists the rooms with their capacity, and collapses again', async () => {
    mockApi(routes)
    const { user } = await open()

    expect(screen.getByText(/Lab 2 \(1:1\)/)).toBeInTheDocument()
    expect(screen.getByText('· capacity 15')).toBeInTheDocument()

    await user.click(screen.getByLabelText('Collapse'))
    expect(screen.queryByText(/Lab 1/)).not.toBeInTheDocument()
  })

  it('adds a room', async () => {
    const api = mockApi({ ...routes, 'POST /branches/b1/rooms': { id: 'r3' } })
    const { user } = await open()

    await user.type(screen.getByPlaceholderText('Room name'), 'Lab 3')
    await user.clear(screen.getByPlaceholderText('Capacity'))
    await user.type(screen.getByPlaceholderText('Capacity'), '12')
    await user.click(screen.getByRole('button', { name: /^Add$/ }))

    await waitFor(() => expect(api.made('POST')).toHaveLength(1))
    expect(api.made('POST')[0].body).toEqual({ name: 'Lab 3', capacity: 12 })
  })

  it('edits a room in place', async () => {
    const api = mockApi({ ...routes, 'PUT /rooms/r1': { id: 'r1' } })
    const { user } = await open()

    await user.click(screen.getByText(/Lab 1/))
    const nameField = screen.getByDisplayValue('Lab 1')
    await user.clear(nameField)
    await user.type(nameField, 'Lab 1B')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.made('PUT')).toHaveLength(1))
    expect(api.made('PUT')[0].body).toEqual({ name: 'Lab 1B', capacity: 15 })
  })

  it('cancels an edit without saving', async () => {
    const api = mockApi(routes)
    const { user } = await open()

    await user.click(screen.getByText(/Lab 1/))
    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(api.made('PUT')).toHaveLength(0)
    expect(screen.getByText(/Lab 1/)).toBeInTheDocument()
  })

  it('deletes a room, and explains why when it cannot be deleted', async () => {
    const alert = vi.spyOn(window, 'alert').mockImplementation(() => {})
    const api = mockApi({
      ...routes,
      'DELETE /rooms/r1': respond(400, { title: 'Bad request', errors: ['Cannot delete a room that has sessions scheduled in it.'] }),
      'DELETE /rooms/r2': respond(204),
    })
    const { user } = await open()

    await user.click(screen.getByLabelText('Delete Lab 1'))
    await waitFor(() => expect(alert).toHaveBeenCalledWith('Cannot delete a room that has sessions scheduled in it.'))

    await user.click(screen.getByLabelText('Delete Lab 2 (1:1)'))
    await waitFor(() => expect(api.made('DELETE')).toHaveLength(2))
  })

  it('shows an empty state for a branch with no rooms', async () => {
    mockApi({ ...routes, 'GET /branches/b1/rooms': [] })
    const { user } = renderApp(<BranchesPage />)

    await user.click((await screen.findAllByLabelText('Expand'))[0])

    expect(await screen.findByText('No rooms yet.')).toBeInTheDocument()
  })
})

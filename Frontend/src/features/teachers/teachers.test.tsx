import { screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mockApi, renderApp, respond } from '@/test/harness'
import { TeachersPage } from './TeachersPage'

const SMOUHA = { id: 'b-smouha', name: 'CodeCamp Smouha', address: '', phone: '', isActive: true }
const KAFR = { id: 'b-kafr', name: 'CodeCamp Kafr Abdo', address: '', phone: '', isActive: true }

const ahmed = { id: 't1', userId: 'u1', fullName: 'Ahmed Nabil', email: 'ahmed@codecamp.demo', hireDate: '2024-01-01', payType: 'Hourly', payRate: 150, branchIds: [SMOUHA.id] }
const sara = { id: 't2', userId: 'u2', fullName: 'Sara Ibrahim', email: 'sara@codecamp.demo', hireDate: '2024-06-01', payType: 'Percentage', payRate: 40, branchIds: [KAFR.id] }

const python = { id: 'c1', name: 'Python Fundamentals', deliveryMode: 'Group', curriculumId: 'cur', branchId: SMOUHA.id }
const scratch = { id: 'c2', name: 'Scratch', deliveryMode: 'Group', curriculumId: 'cur', branchId: SMOUHA.id }

const routes = {
  'GET /teachers': [ahmed, sara],
  'GET /branches': [SMOUHA, KAFR],
  'GET /teachers/candidates': [{ userId: 'u9', fullName: 'Youssef Ali', email: 'youssef@codecamp.demo' }],
  'GET /teachers/t1/availability': [{ id: 'av1', teacherId: 't1', branchId: SMOUHA.id, dayOfWeek: 'Monday', startTime: '09:00:00', endTime: '17:00:00' }],
  'GET /teachers/t1/qualifications': [{ teacherId: 't1', courseId: 'c1', courseName: 'Python Fundamentals' }],
  'GET /courses': [python, scratch],
}

afterEach(() => vi.restoreAllMocks())

describe('TeachersPage', () => {
  it('lists teachers with their pay terms', async () => {
    mockApi(routes)
    renderApp(<TeachersPage />)

    expect(await screen.findByText('Ahmed Nabil')).toBeInTheDocument()
    expect(screen.getByText('Hourly · 150')).toBeInTheDocument()
    expect(screen.getByText('Percentage · 40%')).toBeInTheDocument()
  })

  it('shows no pay badge when the server withholds pay (the front desk is not sent it)', async () => {
    mockApi({ ...routes, 'GET /teachers': [{ ...ahmed, payType: null, payRate: null }] })
    renderApp(<TeachersPage />, { roles: ['FrontDesk'], branchIds: [SMOUHA.id] })

    expect(await screen.findByText('Ahmed Nabil')).toBeInTheDocument()
    expect(screen.queryByText(/Hourly/)).not.toBeInTheDocument()
    expect(screen.queryByText(/null/)).not.toBeInTheDocument()
  })

  it('searches by name or email', async () => {
    mockApi(routes)
    const { user } = renderApp(<TeachersPage />)
    await screen.findByText('Ahmed Nabil')

    await user.type(screen.getByLabelText('Search teachers'), 'sara@')

    expect(screen.queryByText('Ahmed Nabil')).not.toBeInTheDocument()
    expect(screen.getByText('Sara Ibrahim')).toBeInTheDocument()
  })

  it('says when there are none, and when the list cannot be loaded', async () => {
    mockApi({ ...routes, 'GET /teachers': [] })
    const { unmount } = renderApp(<TeachersPage />)
    expect(await screen.findByText('No teacher profiles yet.')).toBeInTheDocument()
    unmount()

    mockApi({ ...routes, 'GET /teachers': respond(500) })
    renderApp(<TeachersPage />)
    expect(await screen.findByText('Could not load teachers.')).toBeInTheDocument()
  })

  it('lets only the Owner edit or delete a profile (pay is sensitive), while a Branch Manager can still create one', async () => {
    mockApi(routes)
    renderApp(<TeachersPage />, { roles: ['BranchManager'], branchIds: [SMOUHA.id] })
    await screen.findByText('Ahmed Nabil')

    expect(screen.queryByLabelText('Edit Ahmed Nabil')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Delete Ahmed Nabil')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /New teacher profile/ })).toBeInTheDocument()
  })

  it('gives a Front Desk user a read-only list', async () => {
    mockApi(routes)
    renderApp(<TeachersPage />, { roles: ['FrontDesk'], branchIds: [SMOUHA.id] })
    await screen.findByText('Ahmed Nabil')

    expect(screen.queryByRole('button', { name: /New teacher profile/ })).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Edit Ahmed Nabil')).not.toBeInTheDocument()
  })

  it('deletes a profile after confirmation, and explains a refusal instead of failing silently', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const alert = vi.spyOn(window, 'alert').mockImplementation(() => {})
    mockApi({
      ...routes,
      'DELETE /teachers/t1': respond(400, { title: 'Bad request', errors: ['Cannot delete a teacher who has scheduled sessions or payroll runs on record.'] }),
    })
    const { user } = renderApp(<TeachersPage />)

    await user.click(await screen.findByLabelText('Delete Ahmed Nabil'))

    await waitFor(() => expect(alert).toHaveBeenCalledWith('Cannot delete a teacher who has scheduled sessions or payroll runs on record.'))
  })

  it('does not delete when the confirmation is declined', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false)
    const api = mockApi(routes)
    const { user } = renderApp(<TeachersPage />)

    await user.click(await screen.findByLabelText('Delete Ahmed Nabil'))

    expect(api.made('DELETE')).toHaveLength(0)
  })

  describe('creating and editing a profile', () => {
    it('creates a profile for a staff account that has the Teacher role but no profile yet', async () => {
      const api = mockApi({ ...routes, 'POST /teachers': { ...ahmed, id: 't9' } })
      const { user } = renderApp(<TeachersPage />)
      await screen.findByText('Ahmed Nabil')

      await user.click(screen.getByRole('button', { name: /New teacher profile/ }))
      await user.selectOptions(await screen.findByLabelText('Staff account'), 'u9')
      await user.type(screen.getByLabelText('Hire date'), '2025-03-01')
      await user.clear(screen.getByLabelText('Pay rate (per hour)'))
      await user.type(screen.getByLabelText('Pay rate (per hour)'), '175')
      await user.click(screen.getByRole('button', { name: 'Save' }))

      await waitFor(() => expect(api.made('POST', '/teachers')).toHaveLength(1))
      expect(api.made('POST', '/teachers')[0].body).toEqual({ userId: 'u9', hireDate: '2025-03-01', payType: 'Hourly', payRate: 175 })
    })

    it('asks for a staff account before saving a new profile', async () => {
      const api = mockApi(routes)
      const { user } = renderApp(<TeachersPage />)
      await screen.findByText('Ahmed Nabil')

      await user.click(screen.getByRole('button', { name: /New teacher profile/ }))
      await user.type(await screen.findByLabelText('Hire date'), '2025-03-01')
      await user.clear(screen.getByLabelText('Pay rate (per hour)'))
      await user.type(screen.getByLabelText('Pay rate (per hour)'), '100')
      await user.click(screen.getByRole('button', { name: 'Save' }))

      expect(await screen.findByText('Select a staff account.')).toBeInTheDocument()
      expect(api.made('POST', '/teachers')).toHaveLength(0)
    })

    it('rejects a percentage above 100 and relabels the rate for each pay type', async () => {
      const api = mockApi(routes)
      const { user } = renderApp(<TeachersPage />)
      await screen.findByText('Ahmed Nabil')

      await user.click(screen.getByRole('button', { name: /New teacher profile/ }))
      await user.selectOptions(await screen.findByLabelText('Pay type'), 'Percentage')
      expect(screen.getByLabelText('Pay rate (% of package revenue collected)')).toBeInTheDocument()
      await user.selectOptions(screen.getByLabelText('Staff account'), 'u9')
      await user.type(screen.getByLabelText('Hire date'), '2025-03-01')
      await user.clear(screen.getByLabelText('Pay rate (% of package revenue collected)'))
      await user.type(screen.getByLabelText('Pay rate (% of package revenue collected)'), '101')
      await user.click(screen.getByRole('button', { name: 'Save' }))

      expect(await screen.findByText('A percentage pay rate cannot exceed 100')).toBeInTheDocument()
      expect(api.made('POST', '/teachers')).toHaveLength(0)

      await user.selectOptions(screen.getByLabelText('Pay type'), 'Fixed')
      expect(screen.getByLabelText('Pay rate (flat, per payroll period)')).toBeInTheDocument()
      expect(screen.getByText(/no matter how many sessions they held/)).toBeInTheDocument()
    })

    it("edits a profile, starting from the teacher's current terms", async () => {
      const api = mockApi({ ...routes, 'PUT /teachers/t1': { ...ahmed, payRate: 200 } })
      const { user } = renderApp(<TeachersPage />)

      await user.click(await screen.findByLabelText('Edit Ahmed Nabil'))
      expect(screen.getByLabelText('Hire date')).toHaveValue('2024-01-01')
      expect(screen.getByLabelText('Pay rate (per hour)')).toHaveValue(150)
      await user.clear(screen.getByLabelText('Pay rate (per hour)'))
      await user.type(screen.getByLabelText('Pay rate (per hour)'), '200')
      await user.click(screen.getByRole('button', { name: 'Save' }))

      await waitFor(() => expect(api.made('PUT')).toHaveLength(1))
      expect(api.made('PUT')[0].body).toEqual({ hireDate: '2024-01-01', payType: 'Hourly', payRate: 200 })
    })

    it('shows an error and keeps the dialog open when saving fails', async () => {
      mockApi({ ...routes, 'PUT /teachers/t1': respond(500) })
      const { user } = renderApp(<TeachersPage />)

      await user.click(await screen.findByLabelText('Edit Ahmed Nabil'))
      await user.click(screen.getByRole('button', { name: 'Save' }))

      expect(await screen.findByText('Could not save the teacher profile.')).toBeInTheDocument()
      expect(screen.getByRole('heading', { name: 'Edit teacher profile' })).toBeInTheDocument()
    })
  })

  describe('an expanded teacher', () => {
    it('shows branches, availability and declared qualifications', async () => {
      mockApi(routes)
      const { user } = renderApp(<TeachersPage />)

      await user.click((await screen.findAllByLabelText('Expand'))[0])

      expect(await screen.findByText('Monday · 09:00–17:00 ·')).toBeInTheDocument()
      expect(await screen.findByText('Python Fundamentals')).toBeInTheDocument()
      expect(screen.getByLabelText('Remove from CodeCamp Smouha')).toBeInTheDocument()

      await user.click(screen.getByLabelText('Collapse'))
      expect(screen.queryByText('Availability')).not.toBeInTheDocument()
    })

    it('assigns the teacher to another branch and removes them from one', async () => {
      const api = mockApi({ ...routes, 'POST /teachers/t1/branches': respond(204), 'DELETE /teachers/t1/branches/b-smouha': respond(204) })
      const { user } = renderApp(<TeachersPage />)
      await user.click((await screen.findAllByLabelText('Expand'))[0])

      await user.selectOptions(await screen.findByDisplayValue('Add to branch...'), KAFR.id)
      await user.click(screen.getAllByRole('button', { name: /^Add$/ })[0])
      await waitFor(() => expect(api.made('POST', 'branches')).toHaveLength(1))
      expect(api.made('POST', 'branches')[0].body).toEqual({ branchId: KAFR.id })

      await user.click(screen.getByLabelText('Remove from CodeCamp Smouha'))
      await waitFor(() => expect(api.made('DELETE', 'branches')).toHaveLength(1))
    })

    it('adds an availability window and removes one', async () => {
      const api = mockApi({
        ...routes,
        'POST /teachers/t1/availability': { id: 'av2' },
        'DELETE /teachers/availability/av1': respond(204),
      })
      const { user } = renderApp(<TeachersPage />)
      await user.click((await screen.findAllByLabelText('Expand'))[0])
      await screen.findByText('Monday · 09:00–17:00 ·')

      await user.selectOptions(screen.getByDisplayValue('Branch'), SMOUHA.id)
      await user.selectOptions(screen.getByDisplayValue('Monday'), 'Wednesday')
      // The page has three Add buttons in order: branch assignment, availability, qualification.
      await user.click(screen.getAllByRole('button', { name: /^Add$/ })[1])

      await waitFor(() => expect(api.made('POST', 'availability')).toHaveLength(1))
      expect(api.made('POST', 'availability')[0].body).toEqual({ branchId: SMOUHA.id, dayOfWeek: 'Wednesday', startTime: '09:00:00', endTime: '17:00:00' })

      await user.click(screen.getByLabelText('Remove availability'))
      await waitFor(() => expect(api.made('DELETE', 'availability')).toHaveLength(1))
    })

    it('declares and revokes a course qualification, offering only courses not yet declared', async () => {
      const api = mockApi({
        ...routes,
        'POST /teachers/t1/qualifications': respond(204),
        'DELETE /teachers/t1/qualifications/c1': respond(204),
      })
      const { user } = renderApp(<TeachersPage />)
      await user.click((await screen.findAllByLabelText('Expand'))[0])
      await screen.findByText('Python Fundamentals')

      const picker = screen.getByDisplayValue('Declare qualified for...')
      expect(picker).toHaveTextContent('Scratch')
      expect(picker).not.toHaveTextContent('Python Fundamentals')
      await user.selectOptions(picker, 'c2')
      await user.click(screen.getAllByRole('button', { name: /^Add$/ }).at(-1)!)
      await waitFor(() => expect(api.made('POST', 'qualifications')).toHaveLength(1))
      expect(api.made('POST', 'qualifications')[0].body).toEqual({ courseId: 'c2' })

      await user.click(screen.getByLabelText('Remove qualification for Python Fundamentals'))
      await waitFor(() => expect(api.made('DELETE', 'qualifications')).toHaveLength(1))
    })

    it('says when a teacher has no branch, availability or qualifications yet', async () => {
      mockApi({
        ...routes,
        'GET /teachers': [{ ...ahmed, branchIds: [] }],
        'GET /teachers/t1/availability': [],
        'GET /teachers/t1/qualifications': [],
      })
      const { user } = renderApp(<TeachersPage />)

      await user.click(await screen.findByLabelText('Expand'))

      expect(await screen.findByText('Not assigned to any branch.')).toBeInTheDocument()
      expect(screen.getByText('No availability set.')).toBeInTheDocument()
      expect(screen.getByText('No declared qualifications yet.')).toBeInTheDocument()
    })
  })
})

import { screen, waitFor, within } from '@testing-library/react'
import { Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mockApi, renderApp, respond } from '@/test/harness'
import { StudentDetailPage } from './StudentDetailPage'
import { StudentsPage } from './StudentsPage'

const SMOUHA = { id: 'b-smouha', name: 'CodeCamp Smouha', address: '', phone: '', isActive: true }
const KAFR = { id: 'b-kafr', name: 'CodeCamp Kafr Abdo', address: '', phone: '', isActive: true }

const khaled = { id: 's1', fullName: 'Khaled Hany', dateOfBirth: '2013-04-12', gender: 'Male', enrollmentDate: '2024-01-01', status: 'Active', currentBranchId: SMOUHA.id }
const malak = { id: 's2', fullName: 'Malak Kamel', dateOfBirth: '2012-11-02', gender: 'Female', enrollmentDate: '2024-01-01', status: 'Paused', currentBranchId: KAFR.id }

afterEach(() => vi.restoreAllMocks())

describe('StudentsPage', () => {
  const routes = { 'GET /students': [khaled, malak], 'GET /branches': [SMOUHA, KAFR] }

  it('lists students with their branch and status', async () => {
    mockApi(routes)
    renderApp(<StudentsPage />)

    expect(await screen.findByText('Khaled Hany')).toBeInTheDocument()
    expect(screen.getByText(/CodeCamp Smouha/)).toBeInTheDocument()
    expect(screen.getByText(/CodeCamp Kafr Abdo/)).toBeInTheDocument()
    expect(screen.getByText('Paused')).toBeInTheDocument()
  })

  it('filters by name as the user types, and says so when nothing matches', async () => {
    mockApi(routes)
    const { user } = renderApp(<StudentsPage />)
    await screen.findByText('Khaled Hany')

    await user.type(screen.getByLabelText('Search students'), 'malak')
    expect(screen.queryByText('Khaled Hany')).not.toBeInTheDocument()
    expect(screen.getByText('Malak Kamel')).toBeInTheDocument()

    await user.clear(screen.getByLabelText('Search students'))
    await user.type(screen.getByLabelText('Search students'), 'zzz')
    expect(screen.getByText('No students match your search.')).toBeInTheDocument()
  })

  it('shows an empty state when there are no students', async () => {
    mockApi({ ...routes, 'GET /students': [] })
    renderApp(<StudentsPage />)

    expect(await screen.findByText('No students yet.')).toBeInTheDocument()
  })

  it('shows an error when the list cannot be loaded', async () => {
    mockApi({ ...routes, 'GET /students': respond(500) })
    renderApp(<StudentsPage />)

    expect(await screen.findByText('Could not load students.')).toBeInTheDocument()
  })

  it('opens a student when their row is clicked', async () => {
    mockApi(routes)
    const { user } = renderApp(<StudentsPage />, { route: '/' })

    await user.click(await screen.findByText('Khaled Hany'))

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/students/s1'))
  })

  it('deletes a student after confirmation', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const api = mockApi({ ...routes, 'DELETE /students/s1': respond(204) })
    const { user } = renderApp(<StudentsPage />)

    await user.click(await screen.findByLabelText('Delete Khaled Hany'))

    await waitFor(() => expect(api.made('DELETE')).toHaveLength(1))
    expect(window.confirm).toHaveBeenCalledWith(expect.stringContaining('Khaled Hany'))
  })

  it('does nothing when the user cancels the confirmation', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false)
    const api = mockApi(routes)
    const { user } = renderApp(<StudentsPage />)

    await user.click(await screen.findByLabelText('Delete Khaled Hany'))

    expect(api.made('DELETE')).toHaveLength(0)
  })

  it("tells the user why a student can't be deleted, instead of silently doing nothing", async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const alert = vi.spyOn(window, 'alert').mockImplementation(() => {})
    mockApi({
      ...routes,
      'DELETE /students/s1': respond(400, { title: 'Bad request', errors: ["This student has enrollments on record and can't be deleted."] }),
    })
    const { user } = renderApp(<StudentsPage />)

    await user.click(await screen.findByLabelText('Delete Khaled Hany'))

    await waitFor(() => expect(alert).toHaveBeenCalledWith("This student has enrollments on record and can't be deleted."))
  })

  it('creates a student with a new guardian, sending exactly what the API expects', async () => {
    const api = mockApi({ ...routes, 'GET /guardians': [], 'POST /students': { ...khaled, id: 's9' } })
    const { user } = renderApp(<StudentsPage />)
    await screen.findByText('Khaled Hany')

    await user.click(screen.getByRole('button', { name: /New student/ }))
    await user.type(screen.getByLabelText('Full name'), 'Omar Tarek')
    await user.type(screen.getByLabelText('Date of birth'), '2011-02-15')
    await user.selectOptions(screen.getByLabelText('Branch'), SMOUHA.id)
    await user.click(screen.getByLabelText('New guardian'))
    await user.type(screen.getByPlaceholderText('Full name'), 'Tarek Aboulfotouh')
    await user.type(screen.getByPlaceholderText('Phone'), '01123450003')
    await user.type(screen.getByPlaceholderText('Email'), 'tarek@example.com')
    await user.click(screen.getByRole('button', { name: 'Create' }))

    await waitFor(() => expect(api.made('POST', '/students')).toHaveLength(1))
    expect(api.made('POST', '/students')[0].body).toMatchObject({
      fullName: 'Omar Tarek',
      dateOfBirth: '2011-02-15',
      branchId: SMOUHA.id,
      existingGuardianId: null,
      newGuardianFullName: 'Tarek Aboulfotouh',
      newGuardianPhone: '01123450003',
      newGuardianEmail: 'tarek@example.com',
      isPrimaryContact: true,
    })
  })

  it('will not create a student without a guardian', async () => {
    const api = mockApi({ ...routes, 'GET /guardians': [] })
    const { user } = renderApp(<StudentsPage />)
    await screen.findByText('Khaled Hany')

    await user.click(screen.getByRole('button', { name: /New student/ }))
    await user.type(screen.getByLabelText('Full name'), 'Omar Tarek')
    await user.type(screen.getByLabelText('Date of birth'), '2011-02-15')
    await user.selectOptions(screen.getByLabelText('Branch'), SMOUHA.id)
    await user.click(screen.getByRole('button', { name: 'Create' }))

    // The placeholder option and the validation message read the same, so wait for the second one.
    await waitFor(() => expect(screen.getAllByText('Select a guardian')).toHaveLength(2))
    expect(api.made('POST', '/students')).toHaveLength(0)
  })

  it('edits an existing student', async () => {
    const api = mockApi({ ...routes, 'PUT /students/s1': { ...khaled, status: 'Graduated' } })
    const { user } = renderApp(<StudentsPage />)

    await user.click(await screen.findByLabelText('Edit Khaled Hany'))
    await user.selectOptions(screen.getByLabelText('Status'), 'Graduated')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.made('PUT')).toHaveLength(1))
    expect(api.made('PUT')[0].body).toEqual({ fullName: 'Khaled Hany', dateOfBirth: '2013-04-12', gender: 'Male', status: 'Graduated' })
  })
})

describe('StudentDetailPage', () => {
  const detailRoutes = {
    'GET /students/s1': khaled,
    'GET /branches': [SMOUHA, KAFR],
    'GET /students/s1/guardians': [{ id: 'g1', fullName: 'Hany Mahmoud', phone: '01123450001', email: 'hany@example.com' }],
    'GET /guardians': [{ id: 'g2', fullName: 'Doaa Kamel', phone: '01123450002', email: 'doaa@example.com' }],
    'GET /students/s1/branch-history': [
      { id: 'h1', studentId: 's1', fromBranchId: KAFR.id, fromBranchName: 'CodeCamp Kafr Abdo', toBranchId: SMOUHA.id, toBranchName: 'CodeCamp Smouha', transferDate: '2025-01-05', reason: 'Family relocated', transferredByUserId: 'u' },
    ],
    'GET /students/s1/enrollments': [{ id: 'e1', studentId: 's1', courseId: 'c1', courseName: 'Python Fundamentals', enrollmentDate: '2024-02-01', status: 'Active', position: null }],
    'GET /students/s1/attendance': [{ id: 'a1', courseSessionId: 'x', courseId: 'c1', courseName: 'Python Fundamentals', sessionStartUtc: '2025-01-06T10:00:00Z', studentId: 's1', studentFullName: 'Khaled Hany', status: 'Present' }],
    'GET /students/s1/grades': [{ id: 'gr1', examId: 'ex1', examName: 'Python Midterm', examMaxScore: 100, examDate: '2025-02-01', courseId: 'c1', courseName: 'Python Fundamentals', studentId: 's1', studentFullName: 'Khaled Hany', score: 88, comments: 'Great grasp of loops' }],
  }

  const renderDetail = (options: Parameters<typeof renderApp>[1] = {}) =>
    renderApp(
      <Routes>
        <Route path="/students/:id" element={<StudentDetailPage />} />
      </Routes>,
      { path: '/students/s1', route: '*', ...options },
    )

  it('shows the whole record to staff: guardians, branch history, enrollments, attendance and grades', async () => {
    mockApi(detailRoutes)
    renderDetail({ roles: ['FrontDesk'], branchIds: [SMOUHA.id] })

    expect(await screen.findByRole('heading', { name: 'Khaled Hany' })).toBeInTheDocument()
    expect(await screen.findByText(/Hany Mahmoud/)).toBeInTheDocument()
    expect(await screen.findByText('CodeCamp Kafr Abdo → CodeCamp Smouha')).toBeInTheDocument()
    expect(screen.getByText(/Family relocated/)).toBeInTheDocument()
    expect((await screen.findAllByText('Python Fundamentals')).length).toBeGreaterThan(0)
    expect(await screen.findByText('88 / 100')).toBeInTheDocument()
    expect(screen.getByText('Present')).toBeInTheDocument()
    expect(screen.getByText(/Great grasp of loops/)).toBeInTheDocument()
  })

  it('shows a teacher only the slice they are allowed: no guardians, branch history or transfer button', async () => {
    const api = mockApi(detailRoutes)
    renderDetail({ roles: ['Teacher'] })

    expect(await screen.findByRole('heading', { name: 'Khaled Hany' })).toBeInTheDocument()
    await screen.findByText('88 / 100')

    expect(screen.queryByText('Guardians')).not.toBeInTheDocument()
    expect(screen.queryByText('Branch history')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Transfer branch/ })).not.toBeInTheDocument()
    expect(api.made('GET', '/guardians')).toHaveLength(0)
    expect(api.made('GET', 'branch-history')).toHaveLength(0)
  })

  it('shows empty states for a student with no history', async () => {
    mockApi({
      ...detailRoutes,
      'GET /students/s1/enrollments': [],
      'GET /students/s1/attendance': [],
      'GET /students/s1/grades': [],
      'GET /students/s1/branch-history': [],
    })
    renderDetail()

    expect(await screen.findByText('No enrollments to show.')).toBeInTheDocument()
    expect(screen.getByText('No attendance records to show.')).toBeInTheDocument()
    expect(screen.getByText('No grades to show.')).toBeInTheDocument()
    expect(screen.getByText('Never transferred branches.')).toBeInTheDocument()
  })

  it('shows a message when the student cannot be loaded', async () => {
    mockApi({ ...detailRoutes, 'GET /students/s1': respond(403) })
    renderDetail()

    expect(await screen.findByText('Could not load this student.')).toBeInTheDocument()
  })

  it('transfers the student to another branch, offering only the other branches', async () => {
    const api = mockApi({ ...detailRoutes, 'POST /students/s1/transfer-branch': { ...khaled, currentBranchId: KAFR.id } })
    const { user } = renderDetail()
    await screen.findByRole('heading', { name: 'Khaled Hany' })

    await user.click(screen.getByRole('button', { name: /Transfer branch/ }))
    const branchSelect = await screen.findByLabelText('New branch')
    expect(within(branchSelect).queryByText('CodeCamp Smouha')).not.toBeInTheDocument()
    await user.selectOptions(branchSelect, KAFR.id)
    await user.type(screen.getByLabelText('Reason (optional)'), 'Moved house')
    await user.click(screen.getByRole('button', { name: 'Transfer' }))

    await waitFor(() => expect(api.made('POST', 'transfer-branch')).toHaveLength(1))
    expect(api.made('POST', 'transfer-branch')[0].body).toEqual({ newBranchId: KAFR.id, reason: 'Moved house' })
  })

  it('shows the server message if the transfer is refused, and keeps the dialog open', async () => {
    mockApi({ ...detailRoutes, 'POST /students/s1/transfer-branch': respond(400, { title: 'Bad request', errors: ['The student is already at this branch.'] }) })
    const { user } = renderDetail()
    await screen.findByRole('heading', { name: 'Khaled Hany' })

    await user.click(screen.getByRole('button', { name: /Transfer branch/ }))
    await user.selectOptions(await screen.findByLabelText('New branch'), KAFR.id)
    await user.click(screen.getByRole('button', { name: 'Transfer' }))

    expect(await screen.findByText('The student is already at this branch.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /Transfer Khaled Hany/ })).toBeInTheDocument()
  })

  it('requires choosing a branch before transferring', async () => {
    const api = mockApi(detailRoutes)
    const { user } = renderDetail()
    await screen.findByRole('heading', { name: 'Khaled Hany' })

    await user.click(screen.getByRole('button', { name: /Transfer branch/ }))
    await user.click(await screen.findByRole('button', { name: 'Transfer' }))

    await waitFor(() => expect(screen.getAllByText('Select a branch')).toHaveLength(2))
    expect(api.made('POST')).toHaveLength(0)
  })

  it('unlinks a guardian and links an existing one', async () => {
    const api = mockApi({
      ...detailRoutes,
      'POST /students/s1/guardians': respond(204),
      'DELETE /students/s1/guardians/g1': respond(204),
    })
    const { user } = renderDetail()
    await screen.findByText(/Hany Mahmoud/)

    await user.click(screen.getByLabelText('Unlink Hany Mahmoud'))
    await waitFor(() => expect(api.made('DELETE')).toHaveLength(1))

    await user.click(screen.getByRole('button', { name: 'Link a guardian' }))
    const options = await screen.findAllByRole('combobox')
    await user.selectOptions(options[0], 'g2')
    await user.click(screen.getByRole('button', { name: 'Link guardian' }))

    await waitFor(() => expect(api.made('POST', '/students/s1/guardians')).toHaveLength(1))
    expect(api.made('POST', '/students/s1/guardians')[0].body).toMatchObject({ guardianId: 'g2', isPrimaryContact: false })
  })

  it('creates a brand-new guardian and links it in one step', async () => {
    const api = mockApi({
      ...detailRoutes,
      'POST /students/s1/guardians': respond(204),
      'POST /guardians': { id: 'g3', fullName: 'New Parent', phone: '0100', email: 'new@example.com' },
    })
    const { user } = renderDetail()
    await screen.findByText(/Hany Mahmoud/)

    await user.click(screen.getByRole('button', { name: 'Link a guardian' }))
    await user.click(screen.getByLabelText('New guardian'))
    await user.type(screen.getByPlaceholderText('Full name'), 'New Parent')
    await user.type(screen.getByPlaceholderText('Phone'), '0100')
    await user.type(screen.getByPlaceholderText('Email'), 'new@example.com')
    await user.click(screen.getByRole('button', { name: 'Link guardian' }))

    await waitFor(() => expect(api.made('POST', '/guardians')).toHaveLength(2))
    expect(api.made('POST', '/guardians')[0].body).toEqual({ fullName: 'New Parent', phone: '0100', email: 'new@example.com' })
    expect(api.made('POST', '/students/s1/guardians')[0].body).toMatchObject({ guardianId: 'g3' })
  })
})

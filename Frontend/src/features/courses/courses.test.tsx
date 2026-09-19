import { screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mockApi, renderApp, respond } from '@/test/harness'
import { CurriculaPage } from '@/features/curricula/CurriculaPage'
import { CoursesPage } from './CoursesPage'

const SMOUHA = { id: 'b1', name: 'CodeCamp Smouha', address: '', phone: '', isActive: true }
const KAFR = { id: 'b2', name: 'CodeCamp Kafr Abdo', address: '', phone: '', isActive: true }
const junior = { id: 'cu1', name: 'Junior Coding', description: 'Ages 8-14, block-based first' }
const web = { id: 'cu2', name: 'Web & Software Development', description: 'Python and JavaScript' }

const python = { id: 'c1', name: 'Python Fundamentals - Group A', deliveryMode: 'Group', curriculumId: web.id, branchId: SMOUHA.id }
const oneOnOne = { id: 'c2', name: 'Web Development with JavaScript - 1:1', deliveryMode: 'OneOnOne', curriculumId: web.id, branchId: SMOUHA.id }

const khaled = { id: 's1', fullName: 'Khaled Hany', dateOfBirth: '2013-04-12', gender: 'Male', enrollmentDate: '', status: 'Active', currentBranchId: SMOUHA.id }
const lina = { id: 's2', fullName: 'Lina Hany', dateOfBirth: '2014-08-22', gender: 'Female', enrollmentDate: '', status: 'Active', currentBranchId: SMOUHA.id }
const malak = { id: 's3', fullName: 'Malak Kamel', dateOfBirth: '2012-11-02', gender: 'Female', enrollmentDate: '', status: 'Active', currentBranchId: KAFR.id }

const routes = {
  'GET /courses': [python, oneOnOne],
  'GET /branches': [SMOUHA, KAFR],
  'GET /curricula': [junior, web],
  'GET /students': [khaled, lina, malak],
}

afterEach(() => vi.restoreAllMocks())

describe('CoursesPage', () => {
  it('lists courses with curriculum, branch and delivery mode', async () => {
    mockApi(routes)
    renderApp(<CoursesPage />)

    expect(await screen.findByText('Python Fundamentals - Group A')).toBeInTheDocument()
    expect((await screen.findAllByText(/Web & Software Development · CodeCamp Smouha/)).length).toBe(2)
    expect(screen.getByText('Group')).toBeInTheDocument()
    expect(screen.getByText('One-on-one')).toBeInTheDocument()
  })

  it('searches, and shows empty and error states', async () => {
    mockApi(routes)
    const { user, unmount } = renderApp(<CoursesPage />)
    await screen.findByText('Python Fundamentals - Group A')

    await user.type(screen.getByLabelText('Search courses'), 'javascript')
    expect(screen.queryByText('Python Fundamentals - Group A')).not.toBeInTheDocument()
    await user.clear(screen.getByLabelText('Search courses'))
    await user.type(screen.getByLabelText('Search courses'), 'zzz')
    expect(screen.getByText('No courses match your search.')).toBeInTheDocument()
    unmount()

    mockApi({ ...routes, 'GET /courses': [] })
    const second = renderApp(<CoursesPage />)
    expect(await screen.findByText('No courses yet.')).toBeInTheDocument()
    second.unmount()

    mockApi({ ...routes, 'GET /courses': respond(500) })
    renderApp(<CoursesPage />)
    expect(await screen.findByText('Could not load courses.')).toBeInTheDocument()
  })

  it('hides create, edit and delete from a Front Desk user', async () => {
    mockApi(routes)
    renderApp(<CoursesPage />, { roles: ['FrontDesk'], branchIds: [SMOUHA.id] })
    await screen.findByText('Python Fundamentals - Group A')

    expect(screen.queryByRole('button', { name: /New course/ })).not.toBeInTheDocument()
    expect(screen.queryByLabelText(/^Edit /)).not.toBeInTheDocument()
    expect(screen.queryByLabelText(/^Delete /)).not.toBeInTheDocument()
  })

  it('creates a course, choosing its curriculum and branch', async () => {
    const api = mockApi({ ...routes, 'POST /courses': { ...python, id: 'c9' } })
    const { user } = renderApp(<CoursesPage />)
    await screen.findByText('Python Fundamentals - Group A')

    await user.click(screen.getByRole('button', { name: /New course/ }))
    await user.type(screen.getByLabelText('Name'), 'Scratch Programming - Group A')
    await user.selectOptions(screen.getByLabelText('Curriculum'), junior.id)
    await user.selectOptions(screen.getByLabelText('Branch'), KAFR.id)
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.made('POST')).toHaveLength(1))
    expect(api.made('POST')[0].body).toEqual({ name: 'Scratch Programming - Group A', deliveryMode: 'Group', curriculumId: junior.id, branchId: KAFR.id })
  })

  it('requires a name, curriculum and branch', async () => {
    const api = mockApi(routes)
    const { user } = renderApp(<CoursesPage />)
    await screen.findByText('Python Fundamentals - Group A')

    await user.click(screen.getByRole('button', { name: /New course/ }))
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('Name is required')).toBeInTheDocument()
    expect(screen.getByText('Curriculum is required')).toBeInTheDocument()
    expect(screen.getByText('Branch is required')).toBeInTheDocument()
    expect(api.made('POST')).toHaveLength(0)
  })

  it('edits only the name and mode of an existing course (branch and curriculum are fixed)', async () => {
    const api = mockApi({ ...routes, 'PUT /courses/c1': python })
    const { user } = renderApp(<CoursesPage />)

    await user.click(await screen.findByLabelText('Edit Python Fundamentals - Group A'))
    expect(screen.queryByLabelText('Curriculum')).not.toBeInTheDocument()
    await user.selectOptions(screen.getByLabelText('Delivery mode'), 'OneOnOne')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.made('PUT')).toHaveLength(1))
    expect(api.made('PUT')[0].body).toEqual({ name: 'Python Fundamentals - Group A', deliveryMode: 'OneOnOne' })
  })

  it('shows why a course could not be saved', async () => {
    mockApi({ ...routes, 'PUT /courses/c1': respond(403, { title: 'You do not have access to this branch.' }) })
    const { user } = renderApp(<CoursesPage />)

    await user.click(await screen.findByLabelText('Edit Python Fundamentals - Group A'))
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('You do not have access to this branch.')).toBeInTheDocument()
  })

  it('deletes a course after confirmation, and reports a refusal', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const alert = vi.spyOn(window, 'alert').mockImplementation(() => {})
    mockApi({
      ...routes,
      'DELETE /courses/c1': respond(400, { title: 'Bad request', errors: ['Cannot delete a course that still has enrollments.'] }),
      'DELETE /courses/c2': respond(204),
    })
    const { user } = renderApp(<CoursesPage />)

    await user.click(await screen.findByLabelText('Delete Python Fundamentals - Group A'))
    await waitFor(() => expect(alert).toHaveBeenCalledWith('Cannot delete a course that still has enrollments.'))
  })

  describe('enrollments and the waitlist', () => {
    const enrollments = [
      { id: 'e1', studentId: 's1', courseId: 'c1', courseName: python.name, enrollmentDate: '2024-01-01', status: 'Active', position: null },
      { id: 'e2', studentId: 's2', courseId: 'c1', courseName: python.name, enrollmentDate: '2024-01-02', status: 'Waitlisted', position: 1 },
      { id: 'e3', studentId: 's9', courseId: 'c1', courseName: python.name, enrollmentDate: '2024-01-03', status: 'Dropped', position: null },
    ]

    const open = async () => {
      const view = renderApp(<CoursesPage />)
      await view.user.click((await screen.findAllByLabelText('Expand'))[0])
      await screen.findByText('Khaled Hany')
      return view
    }

    it('shows active and waitlisted students (with their position) and hides dropped ones', async () => {
      mockApi({ ...routes, 'GET /courses/c1/enrollments': enrollments })
      await open()

      expect(screen.getByText('Lina Hany')).toBeInTheDocument()
      expect(screen.getByText(/position 1/)).toBeInTheDocument()
      expect(screen.getByText('Waitlisted')).toBeInTheDocument()
      expect(screen.queryByText('Unknown student')).not.toBeInTheDocument()
    })

    it('offers only students from the same branch who are not already enrolled', async () => {
      mockApi({ ...routes, 'GET /courses/c1/enrollments': enrollments })
      await open()

      const picker = screen.getByDisplayValue('Select a student from this branch...')
      expect(picker).not.toHaveTextContent('Khaled Hany')   // already active
      expect(picker).not.toHaveTextContent('Lina Hany')     // already waitlisted
      expect(picker).not.toHaveTextContent('Malak Kamel')   // a different branch
    })

    it('enrolls a student, and only enables the button once one is chosen', async () => {
      const api = mockApi({ ...routes, 'GET /courses/c1/enrollments': [], 'POST /courses/c1/enrollments': { id: 'e9' } })
      const { user } = renderApp(<CoursesPage />)
      await user.click((await screen.findAllByLabelText('Expand'))[0])
      const enroll = await screen.findByRole('button', { name: 'Enroll' })
      expect(enroll).toBeDisabled()

      await user.selectOptions(screen.getByDisplayValue('Select a student from this branch...'), 's1')
      expect(enroll).toBeEnabled()
      await user.click(enroll)

      await waitFor(() => expect(api.made('POST')).toHaveLength(1))
      expect(api.made('POST')[0].body).toEqual({ studentId: 's1' })
    })

    it('tells the user when the enrollment is refused', async () => {
      const alert = vi.spyOn(window, 'alert').mockImplementation(() => {})
      mockApi({
        ...routes,
        'GET /courses/c1/enrollments': [],
        'POST /courses/c1/enrollments': respond(400, { title: 'Bad request', errors: ['This student is already enrolled or waitlisted for this course.'] }),
      })
      const { user } = renderApp(<CoursesPage />)
      await user.click((await screen.findAllByLabelText('Expand'))[0])
      await user.selectOptions(await screen.findByDisplayValue('Select a student from this branch...'), 's1')
      await user.click(screen.getByRole('button', { name: 'Enroll' }))

      await waitFor(() => expect(alert).toHaveBeenCalledWith('This student is already enrolled or waitlisted for this course.'))
    })

    it('promotes a waitlisted student and drops an enrollment', async () => {
      const api = mockApi({
        ...routes,
        'GET /courses/c1/enrollments': enrollments,
        'POST /courses/enrollments/e2/promote': { id: 'e2' },
        'DELETE /courses/enrollments/e1': respond(204),
      })
      const { user } = await open()

      await user.click(screen.getByLabelText('Promote from waitlist'))
      await waitFor(() => expect(api.made('POST', 'promote')).toHaveLength(1))

      await user.click(screen.getAllByLabelText('Drop enrollment')[0])
      await waitFor(() => expect(api.made('DELETE')).toHaveLength(1))
    })

    it('says when nobody is enrolled yet', async () => {
      mockApi({ ...routes, 'GET /courses/c1/enrollments': [] })
      const { user } = renderApp(<CoursesPage />)

      await user.click((await screen.findAllByLabelText('Expand'))[0])

      expect(await screen.findByText('No students enrolled yet.')).toBeInTheDocument()
    })
  })
})

describe('CurriculaPage', () => {
  const curriculaRoutes = { 'GET /curricula': [junior, web] }

  it('lists curricula and searches by name or description', async () => {
    mockApi(curriculaRoutes)
    const { user } = renderApp(<CurriculaPage />)

    expect(await screen.findByText('Junior Coding')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Search curricula'), 'javascript')
    expect(screen.queryByText('Junior Coding')).not.toBeInTheDocument()
    expect(screen.getByText('Web & Software Development')).toBeInTheDocument()

    await user.clear(screen.getByLabelText('Search curricula'))
    await user.type(screen.getByLabelText('Search curricula'), 'zzz')
    expect(screen.getByText('No curricula match your search.')).toBeInTheDocument()
  })

  it('shows empty and error states', async () => {
    mockApi({ 'GET /curricula': [] })
    const { unmount } = renderApp(<CurriculaPage />)
    expect(await screen.findByText('No curricula yet.')).toBeInTheDocument()
    unmount()

    mockApi({ 'GET /curricula': respond(500) })
    renderApp(<CurriculaPage />)
    expect(await screen.findByText('Could not load curricula.')).toBeInTheDocument()
  })

  it('is read-only for people who cannot manage the catalog', async () => {
    mockApi(curriculaRoutes)
    renderApp(<CurriculaPage />, { roles: ['Teacher'] })
    await screen.findByText('Junior Coding')

    expect(screen.queryByRole('button', { name: /New curriculum/ })).not.toBeInTheDocument()
    expect(screen.queryByLabelText(/^Edit /)).not.toBeInTheDocument()
  })

  it('creates, edits and validates a curriculum', async () => {
    const api = mockApi({ ...curriculaRoutes, 'POST /curricula': junior, 'PUT /curricula/cu1': junior })
    const { user } = renderApp(<CurriculaPage />)
    await screen.findByText('Junior Coding')

    await user.click(screen.getByRole('button', { name: /New curriculum/ }))
    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Name is required')).toBeInTheDocument()
    expect(screen.getByText('Description is required')).toBeInTheDocument()

    await user.type(screen.getByLabelText('Name'), 'Data Science')
    await user.type(screen.getByLabelText('Description'), 'Python for data')
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(api.made('POST')).toHaveLength(1))
    expect(api.made('POST')[0].body).toEqual({ name: 'Data Science', description: 'Python for data' })

    await user.click(await screen.findByLabelText('Edit Junior Coding'))
    expect(screen.getByLabelText('Description')).toHaveValue('Ages 8-14, block-based first')
    await user.clear(screen.getByLabelText('Name'))
    await user.type(screen.getByLabelText('Name'), 'Junior Coding+')
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(api.made('PUT')).toHaveLength(1))
    expect(api.made('PUT')[0].body).toMatchObject({ name: 'Junior Coding+' })
  })

  it('shows why a curriculum could not be saved', async () => {
    mockApi({ ...curriculaRoutes, 'POST /curricula': respond(500) })
    const { user } = renderApp(<CurriculaPage />)
    await screen.findByText('Junior Coding')

    await user.click(screen.getByRole('button', { name: /New curriculum/ }))
    await user.type(screen.getByLabelText('Name'), 'X')
    await user.type(screen.getByLabelText('Description'), 'Y')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('Could not save this curriculum.')).toBeInTheDocument()
  })

  it('deletes a curriculum after confirmation, and reports a refusal', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const alert = vi.spyOn(window, 'alert').mockImplementation(() => {})
    mockApi({
      ...curriculaRoutes,
      'DELETE /curricula/cu1': respond(400, { title: 'Bad request', errors: ['Cannot delete a curriculum that still has courses.'] }),
    })
    const { user } = renderApp(<CurriculaPage />)

    await user.click(await screen.findByLabelText('Delete Junior Coding'))

    await waitFor(() => expect(alert).toHaveBeenCalledWith('Cannot delete a curriculum that still has courses.'))
  })
})

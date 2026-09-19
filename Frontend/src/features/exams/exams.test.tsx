import { screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mockApi, renderApp, respond } from '@/test/harness'
import { ExamsPage } from './ExamsPage'

const SMOUHA = { id: 'b1', name: 'CodeCamp Smouha', address: '', phone: '', isActive: true }
const python = { id: 'c1', name: 'Python Fundamentals', deliveryMode: 'Group', curriculumId: 'cu', branchId: SMOUHA.id }
const midterm = { id: 'e1', courseId: 'c1', name: 'Python Midterm', maxScore: 100, examDate: '2030-02-01' }

const gradeRow = (studentId: string, name: string, score: number | null, comments: string | null = null) => ({
  id: score === null ? null : `g-${studentId}`, examId: 'e1', examName: 'Python Midterm', examMaxScore: 100, examDate: '2030-02-01',
  courseId: 'c1', courseName: 'Python Fundamentals', studentId, studentFullName: name, score, comments, gradedAtUtc: null, gradedByUserId: null,
})

const staffRoutes = {
  'GET /courses': [python],
  'GET /branches': [SMOUHA],
  'GET /courses/c1/exams': [midterm],
  'GET /exams/e1/grades': [gradeRow('st1', 'Khaled Hany', 88, 'Great grasp of loops'), gradeRow('st2', 'Lina Hany', null)],
}

afterEach(() => vi.restoreAllMocks())

async function pickCourse(user: ReturnType<typeof renderApp>['user']) {
  await screen.findByRole('option', { name: /Python Fundamentals/ })
  await user.selectOptions(screen.getByRole('combobox'), 'c1')
  await screen.findByText('Python Midterm')
}

describe('ExamsPage', () => {
  it('asks for a course, then lists its exams with date and maximum score', async () => {
    mockApi(staffRoutes)
    const { user } = renderApp(<ExamsPage />)

    expect(await screen.findByText('Choose a course to see its exams.')).toBeInTheDocument()
    await pickCourse(user)

    expect(screen.getByText('2030-02-01 · out of 100')).toBeInTheDocument()
  })

  it('shows an empty state for a course with no exams', async () => {
    mockApi({ ...staffRoutes, 'GET /courses/c1/exams': [] })
    const { user } = renderApp(<ExamsPage />)
    await screen.findByRole('option', { name: /Python Fundamentals/ })

    await user.selectOptions(screen.getByRole('combobox'), 'c1')

    expect(await screen.findByText('No exams yet.')).toBeInTheDocument()
  })

  it('shows the branch next to each course for staff who see several branches', async () => {
    mockApi(staffRoutes)
    renderApp(<ExamsPage />)

    expect(await screen.findByRole('option', { name: 'Python Fundamentals · CodeCamp Smouha' })).toBeInTheDocument()
  })

  it('lets the front desk see exams and grades, but not create, edit, delete or grade them', async () => {
    mockApi(staffRoutes)
    const { user } = renderApp(<ExamsPage />, { roles: ['FrontDesk'], branchIds: [SMOUHA.id] })
    await pickCourse(user)
    await user.click(screen.getByLabelText('Expand'))
    await screen.findByText('Khaled Hany')

    expect(screen.queryByRole('button', { name: /New exam/ })).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Edit Python Midterm')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Delete Python Midterm')).not.toBeInTheDocument()
    expect(screen.getByDisplayValue('88')).toBeDisabled()
    expect(screen.queryByRole('button', { name: 'Save' })).not.toBeInTheDocument()
  })

  describe('as a teacher', () => {
    const teacherRoutes = { 'GET /courses/my-courses': [python], 'GET /courses/c1/exams': [midterm], 'GET /exams/e1/grades': staffRoutes['GET /exams/e1/grades'] }

    it("uses the teacher's own course list and never asks for the forbidden ones", async () => {
      const api = mockApi(teacherRoutes)
      renderApp(<ExamsPage />, { roles: ['Teacher'] })

      expect(await screen.findByRole('option', { name: 'Python Fundamentals' })).toBeInTheDocument()   // no "· branch" suffix for a teacher

      expect(api.calls.map((c) => c.path)).toEqual(['/courses/my-courses'])
    })

    it('can create, edit and delete exams for their own course', async () => {
      const api = mockApi({
        ...teacherRoutes,
        'POST /courses/c1/exams': midterm,
        'PUT /exams/e1': midterm,
        'DELETE /exams/e1': respond(204),
      })
      vi.spyOn(window, 'confirm').mockReturnValue(true)
      const { user } = renderApp(<ExamsPage />, { roles: ['Teacher'] })
      await pickCourse(user)

      await user.click(screen.getByRole('button', { name: /New exam/ }))
      await user.type(screen.getByLabelText('Name'), 'Python Final')
      await user.clear(screen.getByLabelText('Max score'))
      await user.type(screen.getByLabelText('Max score'), '50')
      await user.type(screen.getByLabelText('Exam date'), '2030-06-01')
      await user.click(screen.getByRole('button', { name: 'Save' }))
      await waitFor(() => expect(api.made('POST')).toHaveLength(1))
      expect(api.made('POST')[0].body).toEqual({ name: 'Python Final', maxScore: 50, examDate: '2030-06-01' })

      await user.click(await screen.findByLabelText('Edit Python Midterm'))
      expect(screen.getByLabelText('Max score')).toHaveValue(100)
      await user.clear(screen.getByLabelText('Name'))
      await user.type(screen.getByLabelText('Name'), 'Midterm v2')
      await user.click(screen.getByRole('button', { name: 'Save' }))
      await waitFor(() => expect(api.made('PUT')).toHaveLength(1))
      expect(api.made('PUT')[0].body).toEqual({ name: 'Midterm v2', maxScore: 100, examDate: '2030-02-01' })

      await user.click(await screen.findByLabelText('Delete Python Midterm'))
      await waitFor(() => expect(api.made('DELETE')).toHaveLength(1))
    })
  })

  it('validates the exam form', async () => {
    const api = mockApi(staffRoutes)
    const { user } = renderApp(<ExamsPage />)
    await pickCourse(user)

    await user.click(screen.getByRole('button', { name: /New exam/ }))
    await user.clear(screen.getByLabelText('Max score'))
    await user.type(screen.getByLabelText('Max score'), '0')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('Name is required')).toBeInTheDocument()
    expect(screen.getByText('Max score must be greater than 0')).toBeInTheDocument()
    expect(screen.getByText('Exam date is required')).toBeInTheDocument()
    expect(api.made('POST')).toHaveLength(0)
  })

  it('shows why an exam could not be saved', async () => {
    mockApi({ ...staffRoutes, 'POST /courses/c1/exams': respond(403, { title: 'You do not have access to manage exams for this course.' }) })
    const { user } = renderApp(<ExamsPage />)
    await pickCourse(user)

    await user.click(screen.getByRole('button', { name: /New exam/ }))
    await user.type(screen.getByLabelText('Name'), 'X')
    await user.type(screen.getByLabelText('Exam date'), '2030-06-01')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('You do not have access to manage exams for this course.')).toBeInTheDocument()
  })

  it('declines to delete when the user does not confirm, and explains a refusal', async () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
    const alert = vi.spyOn(window, 'alert').mockImplementation(() => {})
    const api = mockApi({ ...staffRoutes, 'DELETE /exams/e1': respond(403, { title: 'Not allowed.' }) })
    const { user } = renderApp(<ExamsPage />)
    await pickCourse(user)

    await user.click(screen.getByLabelText('Delete Python Midterm'))
    expect(api.made('DELETE')).toHaveLength(0)

    confirm.mockReturnValue(true)
    await user.click(screen.getByLabelText('Delete Python Midterm'))
    await waitFor(() => expect(alert).toHaveBeenCalledWith('Not allowed.'))
  })

  describe('grading', () => {
    const open = async (options: Parameters<typeof renderApp>[1] = {}) => {
      const view = renderApp(<ExamsPage />, options)
      await pickCourse(view.user)
      await view.user.click(screen.getByLabelText('Expand'))
      await screen.findByText('Khaled Hany')
      return view
    }

    it('lists every enrolled student, graded or not, and collapses again', async () => {
      mockApi(staffRoutes)
      const { user } = await open()

      expect(screen.getByText('Grades (out of 100)')).toBeInTheDocument()
      expect(screen.getByDisplayValue('88')).toBeInTheDocument()
      expect(screen.getByDisplayValue('Great grasp of loops')).toBeInTheDocument()
      expect(screen.getByText('Lina Hany')).toBeInTheDocument()

      await user.click(screen.getByLabelText('Collapse'))
      expect(screen.queryByText('Khaled Hany')).not.toBeInTheDocument()
    })

    it("only enables Save once something changed, and not for a blank score", async () => {
      mockApi(staffRoutes)
      const { user } = await open()
      const saves = screen.getAllByRole('button', { name: 'Save' })
      expect(saves[0]).toBeDisabled()
      expect(saves[1]).toBeDisabled()

      await user.type(screen.getAllByPlaceholderText('Comments')[1], 'absent that day')
      expect(saves[1]).toBeDisabled()   // a comment alone is not a grade

      await user.type(screen.getAllByPlaceholderText('Score')[1], '70')
      expect(saves[1]).toBeEnabled()
    })

    it('records a new grade with its comment', async () => {
      const api = mockApi({ ...staffRoutes, 'POST /exams/e1/grades': gradeRow('st2', 'Lina Hany', 70) })
      const { user } = await open()

      await user.type(screen.getAllByPlaceholderText('Score')[1], '70')
      await user.type(screen.getAllByPlaceholderText('Comments')[1], 'Good effort')
      await user.click(screen.getAllByRole('button', { name: 'Save' })[1])

      await waitFor(() => expect(api.made('POST')).toHaveLength(1))
      expect(api.made('POST')[0].body).toEqual({ studentId: 'st2', score: 70, comments: 'Good effort' })
    })

    it('updates an existing grade, sending no comment when it is cleared', async () => {
      const api = mockApi({ ...staffRoutes, 'POST /exams/e1/grades': gradeRow('st1', 'Khaled Hany', 91) })
      const { user } = await open()

      await user.clear(screen.getByDisplayValue('88'))
      await user.type(screen.getAllByPlaceholderText('Score')[0], '91')
      await user.clear(screen.getByDisplayValue('Great grasp of loops'))
      await user.click(screen.getAllByRole('button', { name: 'Save' })[0])

      await waitFor(() => expect(api.made('POST')).toHaveLength(1))
      expect(api.made('POST')[0].body).toEqual({ studentId: 'st1', score: 91, comments: null })
    })

    it("tells the user when a score is rejected (e.g. above the exam's maximum)", async () => {
      const alert = vi.spyOn(window, 'alert').mockImplementation(() => {})
      mockApi({ ...staffRoutes, 'POST /exams/e1/grades': respond(400, { title: 'Bad request', errors: ["Score cannot exceed the exam's maximum score of 100."] }) })
      const { user } = await open()

      await user.type(screen.getAllByPlaceholderText('Score')[1], '101')
      await user.click(screen.getAllByRole('button', { name: 'Save' })[1])

      await waitFor(() => expect(alert).toHaveBeenCalledWith("Score cannot exceed the exam's maximum score of 100."))
    })

    it('shows an empty roster and a load failure', async () => {
      mockApi({ ...staffRoutes, 'GET /exams/e1/grades': [] })
      const first = renderApp(<ExamsPage />)
      await pickCourse(first.user)
      await first.user.click(screen.getByLabelText('Expand'))
      expect(await screen.findByText('No students enrolled in this course.')).toBeInTheDocument()
      first.unmount()

      mockApi({ ...staffRoutes, 'GET /exams/e1/grades': respond(500) })
      const second = renderApp(<ExamsPage />)
      await pickCourse(second.user)
      await second.user.click(screen.getByLabelText('Expand'))
      expect(await screen.findByText('Could not load grades for this exam.')).toBeInTheDocument()
    })
  })
})

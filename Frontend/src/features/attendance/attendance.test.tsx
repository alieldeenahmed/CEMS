import { screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mockApi, renderApp, respond } from '@/test/harness'
import { AttendancePage } from './AttendancePage'

const SMOUHA = { id: 'b1', name: 'CodeCamp Smouha', address: '', phone: '', isActive: true }
const python = { id: 'c1', name: 'Python Fundamentals', deliveryMode: 'Group', curriculumId: 'cu', branchId: SMOUHA.id }

const hoursFromNow = (hours: number) => new Date(Date.now() + hours * 3_600_000).toISOString()
const session = (id: string, startHours: number, lengthHours: number, status = 'Scheduled') => ({
  id, courseId: 'c1', roomId: 'r1', teacherId: 't1',
  startUtc: hoursFromNow(startHours), endUtc: hoursFromNow(startHours + lengthHours),
  status, overridden: false, overrideReason: null, rescheduledToSessionId: null,
})

// Only sessions that have started and ended less than 4 hours ago can have attendance marked.
const markable = session('s-now', -2, 1)
const inProgress = session('s-live', -0.5, 1)
const future = session('s-future', 24, 1)
const tooOld = session('s-old', -48, 1)
const cancelled = session('s-cancelled', -2, 1, 'Cancelled')

const roster = [
  { id: 'a1', courseSessionId: 's-now', courseId: 'c1', courseName: 'Python Fundamentals', sessionStartUtc: markable.startUtc, studentId: 'st1', studentFullName: 'Khaled Hany', status: 'Present', markedAtUtc: null, markedByUserId: null },
  { id: null, courseSessionId: 's-now', courseId: 'c1', courseName: 'Python Fundamentals', sessionStartUtc: markable.startUtc, studentId: 'st2', studentFullName: 'Lina Hany', status: 'Unmarked', markedAtUtc: null, markedByUserId: null },
]

const staffRoutes = {
  'GET /courses': [python],
  'GET /branches': [SMOUHA],
  'GET /courses/c1/sessions': [markable, inProgress, future, tooOld, cancelled],
  'GET /sessions/s-now/attendance': roster,
}

afterEach(() => vi.restoreAllMocks())

async function pick(user: ReturnType<typeof renderApp>['user']) {
  await screen.findByRole('option', { name: /Python Fundamentals/ })
  await user.selectOptions(screen.getAllByRole('combobox')[0], 'c1')
  return screen.findByDisplayValue('Select a session...')
}

describe('AttendancePage for staff', () => {
  it('asks for a course and then a session before showing any roster', async () => {
    mockApi(staffRoutes)
    renderApp(<AttendancePage />, { roles: ['BranchManager'], branchIds: [SMOUHA.id] })

    expect(await screen.findByText('Choose a session to see its roster.')).toBeInTheDocument()
    expect(screen.queryByDisplayValue('Select a session...')).not.toBeInTheDocument()
  })

  it('offers only sessions inside the attendance window: started, not cancelled, and ended under 4 hours ago', async () => {
    mockApi(staffRoutes)
    const { user } = renderApp(<AttendancePage />, { roles: ['BranchManager'], branchIds: [SMOUHA.id] })

    const sessionPicker = await pick(user)

    const options = Array.from(sessionPicker.querySelectorAll('option')).slice(1)
    expect(options).toHaveLength(2)   // the finished one and the one in progress
    expect(options.map((o) => o.textContent)).toEqual(expect.arrayContaining([expect.stringContaining('(Scheduled)')]))
  })

  it('shows the roster with every student, defaulting to Unmarked', async () => {
    mockApi(staffRoutes)
    const { user } = renderApp(<AttendancePage />, { roles: ['BranchManager'], branchIds: [SMOUHA.id] })

    const sessionPicker = await pick(user)
    await user.selectOptions(sessionPicker, 's-now')

    expect(await screen.findByText('Khaled Hany')).toBeInTheDocument()
    const [khaled, lina] = screen.getAllByRole('combobox').slice(-2)
    expect(khaled).toHaveValue('Present')
    expect(lina).toHaveValue('Unmarked')
    expect(screen.getByRole('link', { name: 'Khaled Hany' })).toHaveAttribute('href', '/students/st1')
  })

  it('marks a student and sends the chosen status', async () => {
    const api = mockApi({ ...staffRoutes, 'POST /sessions/s-now/attendance': roster[1] })
    const { user } = renderApp(<AttendancePage />, { roles: ['BranchManager'], branchIds: [SMOUHA.id] })
    const sessionPicker = await pick(user)
    await user.selectOptions(sessionPicker, 's-now')
    await screen.findByText('Lina Hany')

    await user.selectOptions(screen.getAllByRole('combobox').slice(-1)[0], 'Late')

    await waitFor(() => expect(api.made('POST')).toHaveLength(1))
    expect(api.made('POST')[0].body).toEqual({ studentId: 'st2', status: 'Late' })
  })

  it('tells the user when the server refuses the change', async () => {
    const alert = vi.spyOn(window, 'alert').mockImplementation(() => {})
    mockApi({
      ...staffRoutes,
      'POST /sessions/s-now/attendance': respond(400, { title: 'Bad request', errors: ['The attendance window for this session has closed.'] }),
    })
    const { user } = renderApp(<AttendancePage />, { roles: ['BranchManager'], branchIds: [SMOUHA.id] })
    const sessionPicker = await pick(user)
    await user.selectOptions(sessionPicker, 's-now')
    await screen.findByText('Lina Hany')

    await user.selectOptions(screen.getAllByRole('combobox').slice(-1)[0], 'Absent')

    await waitFor(() => expect(alert).toHaveBeenCalledWith('The attendance window for this session has closed.'))
  })

  it('lets the front desk look at a roster but not change it', async () => {
    const api = mockApi(staffRoutes)
    const { user } = renderApp(<AttendancePage />, { roles: ['FrontDesk'], branchIds: [SMOUHA.id] })
    const sessionPicker = await pick(user)
    await user.selectOptions(sessionPicker, 's-now')
    await screen.findByText('Khaled Hany')

    const statusPickers = screen.getAllByRole('combobox').slice(-2)
    expect(statusPickers[0]).toBeDisabled()
    expect(statusPickers[1]).toBeDisabled()
    expect(api.made('POST')).toHaveLength(0)
  })

  it('shows an empty roster and a load failure', async () => {
    mockApi({ ...staffRoutes, 'GET /sessions/s-now/attendance': [] })
    const { user, unmount } = renderApp(<AttendancePage />, { roles: ['Owner'] })
    await user.selectOptions(await pick(user), 's-now')
    expect(await screen.findByText('No students enrolled in this course.')).toBeInTheDocument()
    unmount()

    mockApi({ ...staffRoutes, 'GET /sessions/s-now/attendance': respond(500) })
    const second = renderApp(<AttendancePage />, { roles: ['Owner'] })
    await second.user.selectOptions(await pick(second.user), 's-now')
    expect(await screen.findByText('Could not load attendance for this session.')).toBeInTheDocument()
  })
})

describe('AttendancePage for a teacher', () => {
  const teacherRoutes = { 'GET /teachers/my-schedule': [markable, future, tooOld, cancelled], 'GET /sessions/s-now/attendance': roster }

  it("picks from the teacher's own schedule, without asking for a course", async () => {
    const api = mockApi(teacherRoutes)
    renderApp(<AttendancePage />, { roles: ['Teacher'] })

    const picker = await screen.findByDisplayValue('Select one of your sessions...')
    await screen.findByRole('option', { name: /\(Scheduled\)/ })

    expect(picker.querySelectorAll('option')).toHaveLength(2)   // the placeholder and the one markable session
    expect(api.made('GET', '/courses')).toHaveLength(0)
  })

  it('lets the teacher mark their own session', async () => {
    const api = mockApi({ ...teacherRoutes, 'POST /sessions/s-now/attendance': roster[0] })
    const { user } = renderApp(<AttendancePage />, { roles: ['Teacher'] })

    const picker = await screen.findByDisplayValue('Select one of your sessions...')
    await screen.findByRole('option', { name: /\(Scheduled\)/ })
    await user.selectOptions(picker, 's-now')
    await screen.findByText('Khaled Hany')
    const statusPickers = screen.getAllByRole('combobox').slice(-2)
    expect(statusPickers[0]).toBeEnabled()
    await user.selectOptions(statusPickers[0], 'Excused')

    await waitFor(() => expect(api.made('POST')).toHaveLength(1))
    expect(api.made('POST')[0].body).toEqual({ studentId: 'st1', status: 'Excused' })
  })
})

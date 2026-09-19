import { fireEvent, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mockApi, renderApp, respond } from '@/test/harness'
import { SchedulingPage } from './SchedulingPage'

const SMOUHA = { id: 'b1', name: 'CodeCamp Smouha', address: '', phone: '', isActive: true }
const python = { id: 'c1', name: 'Python Fundamentals', deliveryMode: 'Group', curriculumId: 'cu', branchId: SMOUHA.id }
const ahmed = { id: 't1', userId: 'u1', fullName: 'Ahmed Nabil', email: 'a@x', hireDate: '2024-01-01', payType: 'Hourly', payRate: 150, branchIds: [SMOUHA.id] }
const sara = { id: 't2', userId: 'u2', fullName: 'Sara Ibrahim', email: 's@x', hireDate: '2024-01-01', payType: 'PerSession', payRate: 100, branchIds: [SMOUHA.id] }
const outsider = { ...sara, id: 't3', fullName: 'Kafr Abdo Teacher', branchIds: ['elsewhere'] }

const upcoming = { id: 's1', courseId: 'c1', roomId: 'r1', teacherId: 't1', startUtc: '2030-01-07T10:00:00Z', endUtc: '2030-01-07T11:00:00Z', status: 'Scheduled', overridden: false, overrideReason: null, rescheduledToSessionId: null }
const later = { ...upcoming, id: 's2', startUtc: '2030-01-14T10:00:00Z', endUtc: '2030-01-14T11:00:00Z', overridden: true }
const past = { ...upcoming, id: 's3', startUtc: '2020-01-07T10:00:00Z', endUtc: '2020-01-07T11:00:00Z' }
const cancelled = { ...upcoming, id: 's4', status: 'Cancelled', startUtc: '2030-02-04T10:00:00Z', endUtc: '2030-02-04T11:00:00Z' }

const routes = {
  'GET /courses': [python],
  'GET /branches': [SMOUHA],
  'GET /teachers': [ahmed, sara, outsider],
  'GET /courses/c1/sessions': [upcoming, later, past, cancelled],
  'GET /branches/b1/rooms': [{ id: 'r1', branchId: SMOUHA.id, name: 'Lab 1', capacity: 15 }],
}

afterEach(() => vi.restoreAllMocks())

async function pickCourse(user: ReturnType<typeof renderApp>['user']) {
  await screen.findByRole('option', { name: /Python Fundamentals/ })
  await user.selectOptions(screen.getByRole('combobox'), 'c1')
}

describe('SchedulingPage', () => {
  it('asks the user to choose a course first', async () => {
    mockApi(routes)
    renderApp(<SchedulingPage />)

    expect(await screen.findByText('Choose a course to see its sessions.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /New session/ })).not.toBeInTheDocument()
  })

  it("lists a course's sessions with the teacher, status, and a note on overrides", async () => {
    mockApi(routes)
    const { user } = renderApp(<SchedulingPage />)

    await pickCourse(user)

    expect(await screen.findByText('2030-01-07 10:00 – 2030-01-07 11:00')).toBeInTheDocument()
    expect(screen.getByText(/scheduled with an override/)).toBeInTheDocument()
    expect(screen.getAllByText(/Ahmed Nabil/).length).toBeGreaterThan(0)
    expect(screen.getByText('Cancelled')).toBeInTheDocument()
  })

  it('shows an empty state for a course with no sessions', async () => {
    mockApi({ ...routes, 'GET /courses/c1/sessions': [] })
    const { user } = renderApp(<SchedulingPage />)

    await pickCourse(user)

    expect(await screen.findByText('No sessions scheduled yet.')).toBeInTheDocument()
  })

  it('offers substitution only for sessions still ahead, and cancel only for scheduled ones', async () => {
    mockApi(routes)
    const { user } = renderApp(<SchedulingPage />)
    await pickCourse(user)
    await screen.findByText('2030-01-07 10:00 – 2030-01-07 11:00')

    expect(screen.getAllByRole('button', { name: 'Substitute teacher' })).toHaveLength(2)   // the two upcoming sessions, not the past or cancelled one
    expect(screen.getAllByRole('button', { name: 'Cancel' })).toHaveLength(3)              // every Scheduled session, upcoming or past
  })

  it('cancels a session, optionally linking a makeup session', async () => {
    const api = mockApi({ ...routes, 'POST /sessions/s1/cancel': { ...upcoming, status: 'Cancelled' } })
    const { user } = renderApp(<SchedulingPage />)
    await pickCourse(user)
    await screen.findByText('2030-01-07 10:00 – 2030-01-07 11:00')

    await user.click(screen.getAllByRole('button', { name: 'Cancel' })[0])
    await user.selectOptions(screen.getByDisplayValue('No makeup session'), 's2')
    await user.click(screen.getByRole('button', { name: 'Confirm cancel' }))

    await waitFor(() => expect(api.made('POST', 'cancel')).toHaveLength(1))
    expect(api.made('POST', 'cancel')[0].body).toEqual({ rescheduledToSessionId: 's2' })
  })

  it('can back out of a cancellation, and cancels with no makeup by default', async () => {
    const api = mockApi({ ...routes, 'POST /sessions/s1/cancel': { ...upcoming, status: 'Cancelled' } })
    const { user } = renderApp(<SchedulingPage />)
    await pickCourse(user)
    await screen.findByText('2030-01-07 10:00 – 2030-01-07 11:00')

    await user.click(screen.getAllByRole('button', { name: 'Cancel' })[0])
    await user.click(screen.getByRole('button', { name: 'Dismiss' }))
    expect(api.made('POST')).toHaveLength(0)

    await user.click(screen.getAllByRole('button', { name: 'Cancel' })[0])
    await user.click(screen.getByRole('button', { name: 'Confirm cancel' }))
    await waitFor(() => expect(api.made('POST', 'cancel')).toHaveLength(1))
    expect(api.made('POST', 'cancel')[0].body).toEqual({ rescheduledToSessionId: null })
  })

  it('reports a cancellation that fails', async () => {
    const alert = vi.spyOn(window, 'alert').mockImplementation(() => {})
    mockApi({ ...routes, 'POST /sessions/s1/cancel': respond(403, { title: 'You do not have access to this branch.' }) })
    const { user } = renderApp(<SchedulingPage />)
    await pickCourse(user)
    await screen.findByText('2030-01-07 10:00 – 2030-01-07 11:00')

    await user.click(screen.getAllByRole('button', { name: 'Cancel' })[0])
    await user.click(screen.getByRole('button', { name: 'Confirm cancel' }))

    await waitFor(() => expect(alert).toHaveBeenCalledWith('You do not have access to this branch.'))
  })
})

describe('booking a session', () => {
  const open = async (options: Parameters<typeof renderApp>[1] = {}) => {
    const view = renderApp(<SchedulingPage />, options)
    await pickCourse(view.user)
    await view.user.click(await screen.findByRole('button', { name: /New session/ }))
    await screen.findByRole('option', { name: /Lab 1/ })
    return view
  }

  const fillSlot = async (user: ReturnType<typeof renderApp>['user']) => {
    await user.selectOptions(screen.getByLabelText('Room'), 'r1')
    await user.selectOptions(screen.getByLabelText('Teacher'), 't1')
    fireEvent.change(screen.getByLabelText('Start'), { target: { value: '2030-03-04T10:00' } })
    fireEvent.change(screen.getByLabelText('End'), { target: { value: '2030-03-04T11:00' } })
  }

  it("offers only that branch's rooms and teachers", async () => {
    mockApi(routes)
    await open()

    expect(screen.getByLabelText('Room')).toHaveTextContent('Lab 1 (capacity 15)')
    const teacherPicker = screen.getByLabelText('Teacher')
    expect(teacherPicker).toHaveTextContent('Ahmed Nabil')
    expect(teacherPicker).not.toHaveTextContent('Kafr Abdo Teacher')
  })

  it('books a session, sending the times as UTC', async () => {
    const api = mockApi({ ...routes, 'POST /courses/c1/sessions': upcoming })
    const { user } = await open()

    await fillSlot(user)
    await user.click(screen.getByRole('button', { name: 'Schedule session' }))

    await waitFor(() => expect(api.made('POST', 'sessions')).toHaveLength(1))
    expect(api.made('POST', 'sessions')[0].body).toEqual({
      roomId: 'r1', teacherId: 't1', startUtc: '2030-03-04T10:00:00.000Z', endUtc: '2030-03-04T11:00:00.000Z', override: false, overrideReason: null,
    })
  })

  it('does not submit until the room, teacher and times are chosen', async () => {
    const api = mockApi(routes)
    const { user } = await open()

    await user.click(screen.getByRole('button', { name: 'Schedule session' }))

    expect(api.made('POST')).toHaveLength(0)
  })

  const conflict = respond(409, { title: 'A scheduling conflict was detected', conflicts: ['The room is already booked for an overlapping time slot.', "The session falls outside the teacher's declared availability for this branch."] })

  it('lists every conflict the server found', async () => {
    mockApi({ ...routes, 'POST /courses/c1/sessions': conflict })
    const { user } = await open()

    await fillSlot(user)
    await user.click(screen.getByRole('button', { name: 'Schedule session' }))

    expect(await screen.findByText('Scheduling conflict')).toBeInTheDocument()
    expect(screen.getByText('The room is already booked for an overlapping time slot.')).toBeInTheDocument()
    expect(screen.getByText(/outside the teacher's declared availability/)).toBeInTheDocument()
  })

  it('lets a manager override a conflict, but only after giving a reason', async () => {
    let calls = 0
    const api = mockApi({ ...routes, 'POST /courses/c1/sessions': () => (++calls === 1 ? conflict : upcoming) })
    const { user } = await open({ roles: ['BranchManager'], branchIds: [SMOUHA.id] })

    await fillSlot(user)
    await user.click(screen.getByRole('button', { name: 'Schedule session' }))
    const override = await screen.findByRole('button', { name: 'Schedule anyway' })
    expect(override).toBeDisabled()

    await user.type(screen.getByPlaceholderText('Reason for overriding this conflict'), 'Extra revision class')
    expect(override).toBeEnabled()
    await user.click(override)

    await waitFor(() => expect(api.made('POST', 'sessions')).toHaveLength(2))
    expect(api.made('POST', 'sessions')[1].body).toMatchObject({ override: true, overrideReason: 'Extra revision class' })
  })

  it('tells the front desk they cannot override, and offers no override control', async () => {
    mockApi({ ...routes, 'POST /courses/c1/sessions': conflict })
    const { user } = await open({ roles: ['FrontDesk'], branchIds: [SMOUHA.id] })

    await fillSlot(user)
    await user.click(screen.getByRole('button', { name: 'Schedule session' }))

    expect(await screen.findByText('Only an Owner or Branch Manager can override a scheduling conflict.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Schedule anyway' })).not.toBeInTheDocument()
  })

  it('shows the reason for a refusal that is not a scheduling conflict', async () => {
    mockApi({ ...routes, 'POST /courses/c1/sessions': respond(400, { title: 'Bad request', errors: ["The room does not belong to the course's branch."] }) })
    const { user } = await open()

    await fillSlot(user)
    await user.click(screen.getByRole('button', { name: 'Schedule session' }))

    expect(await screen.findByText("The room does not belong to the course's branch.")).toBeInTheDocument()
  })

  it('shows a permission message rather than a vague failure on a 403', async () => {
    mockApi({ ...routes, 'POST /courses/c1/sessions': respond(403, { title: 'You do not have access to this branch.' }) })
    const { user } = await open()

    await fillSlot(user)
    await user.click(screen.getByRole('button', { name: 'Schedule session' }))

    expect(await screen.findByText('You do not have access to this branch.')).toBeInTheDocument()
  })
})

describe('substituting a teacher', () => {
  const open = async (options: Parameters<typeof renderApp>[1] = {}) => {
    const view = renderApp(<SchedulingPage />, options)
    await pickCourse(view.user)
    await screen.findByText('2030-01-07 10:00 – 2030-01-07 11:00')
    await view.user.click(screen.getAllByRole('button', { name: 'Substitute teacher' })[0])
    await screen.findByRole('heading', { name: 'Substitute teacher' })
    return view
  }

  it("offers the branch's other teachers, never the one already assigned", async () => {
    mockApi(routes)
    await open()

    const picker = screen.getByLabelText('Substitute teacher')
    expect(picker).toHaveTextContent('Sara Ibrahim')
    expect(picker).not.toHaveTextContent('Ahmed Nabil')
    expect(picker).not.toHaveTextContent('Kafr Abdo Teacher')
    expect(screen.getByRole('button', { name: 'Substitute' })).toBeDisabled()
  })

  it('swaps the teacher in place', async () => {
    const api = mockApi({ ...routes, 'POST /sessions/s1/substitute-teacher': { ...upcoming, teacherId: 't2' } })
    const { user } = await open()

    await user.selectOptions(screen.getByLabelText('Substitute teacher'), 't2')
    await user.click(screen.getByRole('button', { name: 'Substitute' }))

    await waitFor(() => expect(api.made('POST', 'substitute-teacher')).toHaveLength(1))
    expect(api.made('POST', 'substitute-teacher')[0].body).toEqual({ newTeacherId: 't2', override: false, overrideReason: null })
  })

  it('shows conflicts and lets a manager override with a reason', async () => {
    let calls = 0
    const api = mockApi({
      ...routes,
      'POST /sessions/s1/substitute-teacher': () => (++calls === 1 ? respond(409, { conflicts: ['The teacher is already booked for an overlapping time slot.'] }) : upcoming),
    })
    const { user } = await open({ roles: ['Owner'] })

    await user.selectOptions(screen.getByLabelText('Substitute teacher'), 't2')
    await user.click(screen.getByRole('button', { name: 'Substitute' }))
    expect(await screen.findByText('The teacher is already booked for an overlapping time slot.')).toBeInTheDocument()

    await user.type(screen.getByPlaceholderText('Reason for overriding this conflict'), 'Emergency cover')
    await user.click(screen.getByRole('button', { name: 'Substitute anyway' }))

    await waitFor(() => expect(api.made('POST', 'substitute-teacher')).toHaveLength(2))
    expect(api.made('POST', 'substitute-teacher')[1].body).toMatchObject({ override: true, overrideReason: 'Emergency cover' })
  })

  it('does not let the front desk override', async () => {
    mockApi({ ...routes, 'POST /sessions/s1/substitute-teacher': respond(409, { conflicts: ['Busy.'] }) })
    const { user } = await open({ roles: ['FrontDesk'], branchIds: [SMOUHA.id] })

    await user.selectOptions(screen.getByLabelText('Substitute teacher'), 't2')
    await user.click(screen.getByRole('button', { name: 'Substitute' }))

    expect(await screen.findByText('Only an Owner or Branch Manager can override a scheduling conflict.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Substitute anyway' })).not.toBeInTheDocument()
  })

  it('shows the reason when the substitution is refused outright', async () => {
    mockApi({ ...routes, 'POST /sessions/s1/substitute-teacher': respond(400, { title: 'Bad request', errors: ['A session that has already started cannot be reassigned.'] }) })
    const { user } = await open()

    await user.selectOptions(screen.getByLabelText('Substitute teacher'), 't2')
    await user.click(screen.getByRole('button', { name: 'Substitute' }))

    expect(await screen.findByText('A session that has already started cannot be reassigned.')).toBeInTheDocument()
  })

  it('closes without changing anything when cancelled', async () => {
    const api = mockApi(routes)
    const { user } = await open()

    // The page's own Cancel buttons come first in the DOM; the dialog's is the last one.
    await user.click(screen.getAllByRole('button', { name: 'Cancel' }).at(-1)!)

    expect(screen.queryByRole('heading', { name: 'Substitute teacher' })).not.toBeInTheDocument()
    expect(api.made('POST')).toHaveLength(0)
  })
})

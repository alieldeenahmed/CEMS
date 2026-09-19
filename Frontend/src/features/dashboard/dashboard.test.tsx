import { screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mockApi, renderApp, respond } from '@/test/harness'
import { HomePage } from './HomePage'

const SMOUHA = { id: 'b1', name: 'CodeCamp Smouha', address: '', phone: '', isActive: true }
const KAFR = { id: 'b2', name: 'CodeCamp Kafr Abdo', address: '', phone: '', isActive: true }

const summary = {
  branchId: null,
  periodStart: '2030-01-01',
  periodEnd: '2030-01-31',
  revenue: { totalInvoiced: 12000, totalCollected: 9000, totalOutstanding: 3000 },
  teacherUtilization: [],
  attendanceTrends: { totalRecords: 40, presentCount: 30, absentCount: 4, lateCount: 4, excusedCount: 2, attendanceRate: 0.85 },
  enrollmentFunnel: { totalStudents: 25, studentsWithAnyEnrollment: 20, studentsWithActiveEnrollment: 18 },
}

const hoursFromNow = (hours: number) => new Date(Date.now() + hours * 3_600_000).toISOString()
const session = (id: string, startHours: number, status = 'Scheduled') => ({
  id, courseId: 'c1', roomId: 'r1', teacherId: 't1',
  startUtc: hoursFromNow(startHours), endUtc: hoursFromNow(startHours + 1),
  status, overridden: false, overrideReason: null, rescheduledToSessionId: null,
})

const createObjectURL = vi.fn(() => 'blob:paystub')

beforeEach(() => {
  Object.assign(URL, { createObjectURL, revokeObjectURL: vi.fn() })
})
afterEach(() => {
  vi.restoreAllMocks()
  createObjectURL.mockClear()
})

const card = (label: string) => screen.getByText(label, { selector: 'p.text-sm' }).closest('div.rounded-lg') as HTMLElement

describe('HomePage', () => {
  it('greets the signed-in user by name', async () => {
    mockApi({ 'GET /analytics/dashboard': summary })
    renderApp(<HomePage />, { roles: ['Owner'], fullName: 'Ali Eldeen' })

    expect(screen.getByText(/Welcome back, Ali Eldeen/)).toBeInTheDocument()
    await screen.findByText('$9,000.00')
  })

  describe('as the owner', () => {
    it('shows revenue, outstanding balance, attendance and active enrollments across all branches', async () => {
      const api = mockApi({ 'GET /analytics/dashboard': summary })
      renderApp(<HomePage />, { roles: ['Owner'] })

      expect(screen.getByText('Loading dashboard...')).toBeInTheDocument()
      expect(await screen.findByText('$9,000.00')).toBeInTheDocument()
      expect(screen.getByText('$3,000.00')).toBeInTheDocument()
      expect(screen.getByText('85.0%')).toBeInTheDocument()
      expect(screen.getByText('18')).toBeInTheDocument()
      expect(api.made('GET', '/analytics/dashboard')[0].params?.branchId).toBeUndefined()
    })

    it('reports a failure to load', async () => {
      mockApi({ 'GET /analytics/dashboard': respond(500) })
      renderApp(<HomePage />, { roles: ['Owner'] })

      expect(await screen.findByText('Could not load dashboard data.')).toBeInTheDocument()
    })
  })

  describe('as a branch manager', () => {
    it("scopes the summary to the manager's own branch", async () => {
      const api = mockApi({ 'GET /analytics/dashboard': summary })
      renderApp(<HomePage />, { roles: ['BranchManager'], branchIds: ['b2'] })

      await screen.findByText('$9,000.00')
      expect(api.made('GET', '/analytics/dashboard')[0].params?.branchId).toBe('b2')
    })
  })

  describe('as front desk', () => {
    const routes = {
      'GET /branches': [SMOUHA, KAFR],
      'GET /students': [
        { id: 's1', fullName: 'A', status: 'Active' },
        { id: 's2', fullName: 'B', status: 'Active' },
        { id: 's3', fullName: 'C', status: 'Paused' },
      ],
      'GET /courses': [{ id: 'c1' }, { id: 'c2' }],
      'GET /teachers': [
        { id: 't1', fullName: 'Mona', branchIds: ['b1'] },
        { id: 't2', fullName: 'Omar', branchIds: ['b2'] },
        { id: 't3', fullName: 'Rana', branchIds: ['b1', 'b2'] },
      ],
    }

    it('counts active students, courses, and only the teachers at their own branch', async () => {
      mockApi(routes)
      renderApp(<HomePage />, { roles: ['FrontDesk'], branchIds: ['b1'] })

      expect(await screen.findByText('CodeCamp Smouha')).toBeInTheDocument()
      expect(card('Active students')).toHaveTextContent('2')
      expect(card('Courses offered')).toHaveTextContent('2')
      expect(card('Teachers at your branch')).toHaveTextContent('2')   // Mona and Rana
    })

    it('falls back to a generic label when the branch is unknown', async () => {
      mockApi({ ...routes, 'GET /branches': [] })
      renderApp(<HomePage />, { roles: ['FrontDesk'], branchIds: ['b1'] })

      expect(await screen.findByText('your branch')).toBeInTheDocument()
    })
  })

  describe('as a teacher', () => {
    const routes = {
      'GET /teachers/my-schedule': [session('later', 48), session('soon', 2), session('past', -5), session('cancelled', 3, 'Cancelled')],
      'GET /courses/my-courses': [{ id: 'c1' }, { id: 'c2' }],
      'GET /teachers/my-payroll-runs': [{ id: 'r1', teacherId: 't1', periodStart: '2030-01-01', periodEnd: '2030-01-31', totalAmount: 1500, status: 'Approved' }],
    }

    it('shows only upcoming, scheduled sessions, soonest first', async () => {
      mockApi(routes)
      renderApp(<HomePage />, { roles: ['Teacher'] })

      const soon = await screen.findByText((text) => text.includes(hoursFromNow(2).slice(0, 16).replace('T', ' ')))
      const later = screen.getByText((text) => text.includes(hoursFromNow(48).slice(0, 16).replace('T', ' ')))
      expect(soon.compareDocumentPosition(later) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
      expect(card('Upcoming sessions')).toHaveTextContent('2')
    })

    it('shows courses taught and the latest payroll run', async () => {
      mockApi(routes)
      renderApp(<HomePage />, { roles: ['Teacher'] })

      expect(await screen.findByText('$1500 · Approved')).toBeInTheDocument()
      expect(card('Courses taught')).toHaveTextContent('2')
    })

    it('handles a teacher with nothing scheduled and no payroll yet', async () => {
      mockApi({ 'GET /teachers/my-schedule': [], 'GET /courses/my-courses': [], 'GET /teachers/my-payroll-runs': [] })
      renderApp(<HomePage />, { roles: ['Teacher'] })

      expect(await screen.findByText('No upcoming sessions scheduled.')).toBeInTheDocument()
      expect(screen.getByText('None yet')).toBeInTheDocument()
      expect(screen.queryByRole('button', { name: 'Download latest pay stub' })).not.toBeInTheDocument()
    })

    it('downloads the latest pay stub, and reports a failure', async () => {
      const alert = vi.spyOn(window, 'alert').mockImplementation(() => {})
      const api = mockApi({ ...routes, 'GET /payroll-runs/r1/paystub': {} })
      const { user } = renderApp(<HomePage />, { roles: ['Teacher'] })

      await user.click(await screen.findByRole('button', { name: 'Download latest pay stub' }))
      await waitFor(() => expect(createObjectURL).toHaveBeenCalledTimes(1))
      expect(api.made('GET', 'r1/paystub')).toHaveLength(1)
      expect(alert).not.toHaveBeenCalled()
    })

    it('reports a pay stub download failure', async () => {
      const alert = vi.spyOn(window, 'alert').mockImplementation(() => {})
      mockApi({ ...routes, 'GET /payroll-runs/r1/paystub': respond(500) })
      const { user } = renderApp(<HomePage />, { roles: ['Teacher'] })

      await user.click(await screen.findByRole('button', { name: 'Download latest pay stub' }))

      await waitFor(() => expect(alert).toHaveBeenCalledWith('Could not download this pay stub.'))
    })
  })
})

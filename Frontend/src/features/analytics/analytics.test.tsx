import { fireEvent, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mockApi, renderApp, respond } from '@/test/harness'
import { AnalyticsPage } from './AnalyticsPage'

const SMOUHA = { id: 'b1', name: 'CodeCamp Smouha', address: '', phone: '', isActive: true }
const KAFR = { id: 'b2', name: 'CodeCamp Kafr Abdo', address: '', phone: '', isActive: true }

const summary = {
  branchId: null,
  periodStart: '2030-01-01',
  periodEnd: '2030-01-31',
  revenue: { totalInvoiced: 12000, totalCollected: 9000.5, totalOutstanding: 2999.5 },
  teacherUtilization: [
    { teacherId: 't1', teacherFullName: 'Mona Adel', scheduledHours: 17.142857142857142, availableHours: 40, utilizationRate: 0.42857 },
    { teacherId: 't2', teacherFullName: 'Omar Zaki', scheduledHours: 8, availableHours: 20, utilizationRate: 0.4 },
  ],
  attendanceTrends: { totalRecords: 40, presentCount: 30, absentCount: 4, lateCount: 4, excusedCount: 2, attendanceRate: 0.85 },
  enrollmentFunnel: { totalStudents: 25, studentsWithAnyEnrollment: 20, studentsWithActiveEnrollment: 18 },
}

const createObjectURL = vi.fn(() => 'blob:export')
const revokeObjectURL = vi.fn()

beforeEach(() => {
  Object.assign(URL, { createObjectURL, revokeObjectURL })
})
afterEach(() => {
  vi.restoreAllMocks()
  createObjectURL.mockClear()
})

describe('AnalyticsPage', () => {
  it('shows revenue, attendance, enrollment and teacher utilisation', async () => {
    mockApi({ 'GET /branches': [SMOUHA, KAFR], 'GET /analytics/dashboard': summary })
    renderApp(<AnalyticsPage />, { roles: ['Owner'] })

    expect(await screen.findByText('$12,000.00')).toBeInTheDocument()
    expect(screen.getByText('$9,000.50')).toBeInTheDocument()
    expect(screen.getByText('$2,999.50')).toBeInTheDocument()
    expect(screen.getByText('85.0%')).toBeInTheDocument()

    expect(screen.getByText('Total records').nextSibling).toHaveTextContent('40')
    expect(screen.getByText('Present').nextSibling).toHaveTextContent('30')
    expect(screen.getByText('Total students').nextSibling).toHaveTextContent('25')
    expect(screen.getByText('With active enrollment').nextSibling).toHaveTextContent('18')

    expect(screen.getByText('Mona Adel')).toBeInTheDocument()
    expect(screen.getByText('42.9%')).toBeInTheDocument()
  })

  it('rounds scheduled hours instead of printing floating-point noise', async () => {
    mockApi({ 'GET /branches': [SMOUHA], 'GET /analytics/dashboard': summary })
    renderApp(<AnalyticsPage />, { roles: ['Owner'] })

    expect(await screen.findByText(/17\.1h \/ 40h/)).toBeInTheDocument()
    expect(screen.getByText(/8h \/ 20h/)).toBeInTheDocument()
    expect(screen.queryByText(/17\.142857/)).not.toBeInTheDocument()
  })

  it('shows an empty utilisation state', async () => {
    mockApi({ 'GET /branches': [SMOUHA], 'GET /analytics/dashboard': { ...summary, teacherUtilization: [] } })
    renderApp(<AnalyticsPage />, { roles: ['Owner'] })

    expect(await screen.findByText('No teacher activity in this period.')).toBeInTheDocument()
  })

  it('reports a load failure', async () => {
    mockApi({ 'GET /branches': [SMOUHA], 'GET /analytics/dashboard': respond(500) })
    renderApp(<AnalyticsPage />, { roles: ['Owner'] })

    expect(await screen.findByText('Could not load analytics for this period.')).toBeInTheDocument()
  })

  it('lets the owner scope to a branch, sending it with the request', async () => {
    const api = mockApi({ 'GET /branches': [SMOUHA, KAFR], 'GET /analytics/dashboard': summary })
    const { user } = renderApp(<AnalyticsPage />, { roles: ['Owner'] })
    await screen.findByText('$12,000.00')
    expect(api.made('GET', '/analytics/dashboard')[0].params?.branchId).toBeUndefined()

    await screen.findByRole('option', { name: 'CodeCamp Kafr Abdo' })
    await user.selectOptions(screen.getByLabelText('Branch'), 'b2')

    await waitFor(() => expect(api.made('GET', '/analytics/dashboard').some((c) => c.params?.branchId === 'b2')).toBe(true))
  })

  it('pins a branch manager to their own branch with no picker', async () => {
    const api = mockApi({ 'GET /branches': [SMOUHA, KAFR], 'GET /analytics/dashboard': summary })
    renderApp(<AnalyticsPage />, { roles: ['BranchManager'], branchIds: ['b1'] })

    expect(await screen.findByText('CodeCamp Smouha')).toBeInTheDocument()
    expect(screen.queryByLabelText('Branch')).not.toBeInTheDocument()
    await screen.findByText('$12,000.00')
    expect(api.made('GET', '/analytics/dashboard')[0].params?.branchId).toBe('b1')
  })

  it('re-queries when the period changes', async () => {
    const api = mockApi({ 'GET /branches': [SMOUHA], 'GET /analytics/dashboard': summary })
    renderApp(<AnalyticsPage />, { roles: ['Owner'] })
    await screen.findByText('$12,000.00')

    fireEvent.change(screen.getByLabelText('From'), { target: { value: '2030-01-10' } })

    await waitFor(() => expect(api.made('GET', '/analytics/dashboard').some((c) => c.params?.periodStart === '2030-01-10')).toBe(true))
  })

  it('exports the PDF and the Excel workbook', async () => {
    const api = mockApi({
      'GET /branches': [SMOUHA],
      'GET /analytics/dashboard': summary,
      'GET /analytics/dashboard/export/pdf': {},
      'GET /analytics/dashboard/export/excel': {},
    })
    const { user } = renderApp(<AnalyticsPage />, { roles: ['Owner'] })
    await screen.findByText('$12,000.00')

    await user.click(screen.getByRole('button', { name: 'PDF' }))
    await waitFor(() => expect(api.made('GET', 'export/pdf')).toHaveLength(1))

    await user.click(screen.getByRole('button', { name: 'Excel' }))
    await waitFor(() => expect(api.made('GET', 'export/excel')).toHaveLength(1))
    expect(createObjectURL).toHaveBeenCalledTimes(2)
  })

  it('tells the user when an export fails instead of failing silently', async () => {
    const alert = vi.spyOn(window, 'alert').mockImplementation(() => {})
    mockApi({
      'GET /branches': [SMOUHA],
      'GET /analytics/dashboard': summary,
      'GET /analytics/dashboard/export/pdf': respond(500),
      'GET /analytics/dashboard/export/excel': respond(500),
    })
    const { user } = renderApp(<AnalyticsPage />, { roles: ['Owner'] })
    await screen.findByText('$12,000.00')

    await user.click(screen.getByRole('button', { name: 'PDF' }))
    await waitFor(() => expect(alert).toHaveBeenCalledWith('Could not export the PDF.'))

    await user.click(screen.getByRole('button', { name: 'Excel' }))
    await waitFor(() => expect(alert).toHaveBeenCalledWith('Could not export the spreadsheet.'))
  })
})

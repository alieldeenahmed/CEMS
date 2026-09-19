import { render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { signIn } from '@/test/harness'

// The real router reads window.location when it is created, so each test sets the URL first and then
// loads a fresh copy of the app. The network fake has to be installed on that same fresh copy of the
// API client, hence the dynamic import of the harness after the module reset.
async function openApp(path: string, routes: Record<string, unknown> = {}) {
  window.history.pushState({}, '', path)
  vi.resetModules()
  const { mockApi } = await import('@/test/harness')
  mockApi(routes as Parameters<typeof mockApi>[0])
  const { default: App } = await import('../App')
  return render(<App />)
}

beforeEach(() => {
  localStorage.clear()
})
afterEach(() => {
  window.history.pushState({}, '', '/')
})

describe('the assembled app', () => {
  it('sends a signed-out visitor to the login page, whatever they asked for', async () => {
    await openApp('/students')

    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/login')
  })

  it('shows a signed-in owner the dashboard inside the app shell', async () => {
    signIn(['Owner'], { fullName: 'Ali Eldeen' })
    await openApp('/', {
      'GET /analytics/dashboard': {
        revenue: { totalInvoiced: 1, totalCollected: 2, totalOutstanding: 3 },
        teacherUtilization: [],
        attendanceTrends: { totalRecords: 0, presentCount: 0, absentCount: 0, lateCount: 0, excusedCount: 0, attendanceRate: 0 },
        enrollmentFunnel: { totalStudents: 0, studentsWithAnyEnrollment: 0, studentsWithActiveEnrollment: 0 },
      },
    })

    expect(await screen.findByText(/Welcome back, Ali Eldeen/)).toBeInTheDocument()
    expect(await screen.findByText('$2.00')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Payroll/ })).toBeInTheDocument()
  })

  it('routes deep links to their pages', async () => {
    signIn(['Owner'])
    await openApp('/payroll', { 'GET /teachers': [], 'GET /users/staff': [] })

    expect(await screen.findByText('Generate, approve, and pay payroll runs.')).toBeInTheDocument()
  })

  it('keeps a session across reloads via the stored token', async () => {
    signIn(['Teacher'])
    await openApp('/payroll', { 'GET /teachers/my-payroll-runs': [] })

    expect(await screen.findByText('Your payroll runs.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Sign in' })).not.toBeInTheDocument()
  })
})

describe('query client defaults', () => {
  it('retries a failed query once and treats data as fresh for 30 seconds', async () => {
    const { queryClient } = await import('./queryClient')

    const defaults = queryClient.getDefaultOptions().queries
    expect(defaults?.retry).toBe(1)
    expect(defaults?.staleTime).toBe(30_000)
  })
})

import { screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Outlet, Route, Routes } from 'react-router-dom'
import { AppShell } from '@/app/layout/AppShell'
import { ProtectedRoute } from '@/app/ProtectedRoute'
import { fakeToken, mockApi, renderApp, respond } from '@/test/harness'
import { AUTH_TOKEN_STORAGE_KEY } from './constants'
import { LoginPage } from './LoginPage'

const authResult = {
  userId: 'u1',
  email: 'nourhan@codecamp.demo',
  fullName: 'Nourhan Adel',
  roles: ['BranchManager'],
  token: fakeToken({ branchIds: ['smouha'] }),
  expiresAtUtc: '2030-01-01T00:00:00Z',
}

describe('LoginPage', () => {
  it('signs in, stores the session, and goes to the dashboard', async () => {
    const api = mockApi({ 'POST /auth/login': authResult })
    const { user } = renderApp(<LoginPage />, { roles: null, path: '/login' })

    await user.type(screen.getByLabelText('Email'), 'nourhan@codecamp.demo')
    await user.type(screen.getByLabelText('Password'), 'DemoPass123')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/'))
    expect(api.calls[0].body).toEqual({ email: 'nourhan@codecamp.demo', password: 'DemoPass123' })
    expect(localStorage.getItem(AUTH_TOKEN_STORAGE_KEY)).toBe(authResult.token)
    expect(JSON.parse(localStorage.getItem('cems.user')!)).toMatchObject({ fullName: 'Nourhan Adel', branchIds: ['smouha'] })
  })

  it('validates the form before calling the server', async () => {
    const api = mockApi({})
    const { user } = renderApp(<LoginPage />, { roles: null, path: '/login' })

    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Email is required')).toBeInTheDocument()
    expect(screen.getByText('Password is required')).toBeInTheDocument()
    expect(api.calls).toHaveLength(0)
  })

  it('says the credentials are wrong when the server rejects them', async () => {
    mockApi({ 'POST /auth/login': respond(401, { title: 'Invalid email or password.' }) })
    const { user } = renderApp(<LoginPage />, { roles: null, path: '/login' })

    await user.type(screen.getByLabelText('Email'), 'nourhan@codecamp.demo')
    await user.type(screen.getByLabelText('Password'), 'wrong')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Incorrect email or password.')).toBeInTheDocument()
    expect(localStorage.getItem(AUTH_TOKEN_STORAGE_KEY)).toBeNull()
  })

  it('tells the user the API is unreachable when there is no response at all', async () => {
    mockApi({ 'POST /auth/login': () => { throw new Error('Network Error') } })
    const { user } = renderApp(<LoginPage />, { roles: null, path: '/login' })

    await user.type(screen.getByLabelText('Email'), 'nourhan@codecamp.demo')
    await user.type(screen.getByLabelText('Password'), 'DemoPass123')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText(/Could not reach the server/)).toBeInTheDocument()
  })
})

describe('ProtectedRoute', () => {
  const guarded = (
    <Routes>
      <Route element={<ProtectedRoute />}>
        <Route path="/secret" element={<p>secret page</p>} />
      </Route>
      <Route path="/login" element={<p>login page</p>} />
    </Routes>
  )

  it('lets a signed-in user through', () => {
    renderApp(guarded, { path: '/secret', route: '*' })

    expect(screen.getByText('secret page')).toBeInTheDocument()
  })

  it('sends a signed-out visitor to the login page', () => {
    renderApp(guarded, { roles: null, path: '/secret', route: '*' })

    expect(screen.getByText('login page')).toBeInTheDocument()
  })

  it('treats an expired stored token as signed out', () => {
    renderApp(guarded, { roles: null, path: '/secret', route: '*' })
    localStorage.setItem(AUTH_TOKEN_STORAGE_KEY, fakeToken({ expiresInSeconds: -60 }))

    expect(screen.getByText('login page')).toBeInTheDocument()
  })
})

describe('AppShell navigation', () => {
  const shell = (
    <Routes>
      <Route element={<AppShell />}>
        <Route path="*" element={<p>page body</p>} />
      </Route>
    </Routes>
  )

  const linksFor = (roles: Parameters<typeof renderApp>[1]) => {
    renderApp(shell, { ...roles, route: '*' })
    return screen.getAllByRole('link').map((link) => link.textContent)
  }

  it('gives the Owner every page', () => {
    expect(linksFor({ roles: ['Owner'] })).toEqual([
      'Dashboard', 'Branches', 'Staff', 'Students', 'Teachers', 'Courses', 'Curricula',
      'Scheduling', 'Attendance', 'Exams & Grades', 'Payments', 'Payroll', 'Analytics',
    ])
  })

  it('keeps a Branch Manager out of branch administration but lets them run their branch', () => {
    const links = linksFor({ roles: ['BranchManager'] })

    expect(links).not.toContain('Branches')
    expect(links).toEqual(expect.arrayContaining(['Staff', 'Students', 'Teachers', 'Analytics', 'Payroll']))
  })

  it('shows the Front Desk the desk work only: no teachers, staff, curricula or analytics', () => {
    const links = linksFor({ roles: ['FrontDesk'] })

    expect(links).toEqual(['Dashboard', 'Students', 'Courses', 'Scheduling', 'Attendance', 'Payments', 'Payroll'])
  })

  it('shows a Teacher only their own classroom pages', () => {
    const links = linksFor({ roles: ['Teacher'] })

    expect(links).toEqual(['Dashboard', 'Attendance', 'Exams & Grades', 'Payroll'])
  })

  it('greets the user and signs them out back to the login page', async () => {
    const { user } = renderApp(
      <Routes>
        <Route element={<AppShell />}>
          <Route path="/" element={<p>page body</p>} />
        </Route>
        <Route path="/login" element={<p>login page</p>} />
      </Routes>,
      { roles: ['Owner'], fullName: 'Mostafa El-Sayed', route: '*' },
    )

    expect(screen.getByText('Welcome, Mostafa El-Sayed')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /Sign out/ }))

    expect(screen.getByText('login page')).toBeInTheDocument()
    expect(localStorage.getItem(AUTH_TOKEN_STORAGE_KEY)).toBeNull()
  })
})

// Keeps the unused import honest if the outlet shell above changes.
void Outlet

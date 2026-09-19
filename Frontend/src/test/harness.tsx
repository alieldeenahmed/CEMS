import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, type RenderResult } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AxiosError, type InternalAxiosRequestConfig } from 'axios'
import type { ReactElement } from 'react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { AuthProvider } from '@/features/auth/AuthContext'
import { AUTH_TOKEN_STORAGE_KEY, type Role } from '@/features/auth/constants'
import { apiClient } from '@/shared/api/client'
import { mockState } from './mockState'

// ---- Fake network -------------------------------------------------------------------------------
//
// The real axios client, interceptors, React Query hooks and components all run; only the last hop
// (the HTTP adapter) is replaced. A request with no matching route fails the test, so a missing mock
// can never hide as a silently empty screen.

export interface MockRequest {
  method: string
  path: string
  params?: Record<string, unknown>
  body?: any
}

export class MockResponse {
  status: number
  data: unknown

  constructor(status: number, data: unknown) {
    this.status = status
    this.data = data
  }
}

/** Reply with a specific status, e.g. `respond(409, { conflicts: [...] })`. */
export const respond = (status: number, data: unknown = {}) => new MockResponse(status, data)

type Handler = unknown | ((request: MockRequest) => unknown)

function compile(pattern: string): RegExp {
  const source = pattern.replace(/[.*+?^${}()|[\]\\]/g, '\\$&').replace(/:[A-Za-z]+/g, '[^/]+')
  return new RegExp(`^${source}$`)
}

export function mockApi(routes: Record<string, Handler>) {
  const compiled = Object.entries(routes).map(([key, handler]) => {
    const [method, ...rest] = key.split(' ')
    return { method, regex: compile(rest.join(' ')), handler }
  })
  const calls: MockRequest[] = []
  mockState.unmocked.length = 0

  apiClient.defaults.adapter = async (config: InternalAxiosRequestConfig) => {
    const method = (config.method ?? 'get').toUpperCase()
    const path = config.url ?? ''
    let body: any
    if (typeof config.data === 'string' && config.data.length > 0) {
      body = JSON.parse(config.data)
    } else if (config.data !== undefined) {
      body = config.data
    }
    const request: MockRequest = { method, path, params: config.params, body }
    calls.push(request)

    const route = compiled.find((r) => r.method === method && r.regex.test(path))
    if (!route) {
      mockState.unmocked.push(`${method} ${path}`)
      throw new Error(`Unmocked request: ${method} ${path}`)
    }

    const result = typeof route.handler === 'function' ? (route.handler as (r: MockRequest) => unknown)(request) : route.handler
    const status = result instanceof MockResponse ? result.status : 200
    const data = result instanceof MockResponse ? result.data : result
    const response = { data, status, statusText: String(status), headers: {}, config, request: {} }

    if (status >= 400) {
      throw new AxiosError(`Request failed with status code ${status}`, 'ERR_BAD_REQUEST', config, {}, response)
    }
    return response
  }

  return {
    calls,
    /** Requests made with the given method, optionally narrowed to paths containing `contains`. */
    made: (method: string, contains = '') => calls.filter((c) => c.method === method && c.path.includes(contains)),
  }
}

// ---- Signed-in rendering ------------------------------------------------------------------------

function base64Url(value: object): string {
  return btoa(JSON.stringify(value)).replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_')
}

/** An unsigned JWT with the payload shape the app reads (branch_id, exp). */
export function fakeToken(options: { branchIds?: string[]; expiresInSeconds?: number } = {}): string {
  const payload = {
    sub: 'user-1',
    email: 'test@codecamp.demo',
    full_name: 'Test User',
    branch_id: options.branchIds && options.branchIds.length === 1 ? options.branchIds[0] : options.branchIds,
    exp: Math.floor(Date.now() / 1000) + (options.expiresInSeconds ?? 3600),
  }
  return `${base64Url({ alg: 'HS256', typ: 'JWT' })}.${base64Url(payload)}.signature`
}

export function signIn(roles: Role[] = ['Owner'], options: { branchIds?: string[]; fullName?: string } = {}) {
  const user = {
    userId: 'user-1',
    email: 'test@codecamp.demo',
    fullName: options.fullName ?? 'Test User',
    roles,
    branchIds: options.branchIds ?? [],
  }
  localStorage.setItem(AUTH_TOKEN_STORAGE_KEY, fakeToken({ branchIds: options.branchIds }))
  localStorage.setItem('cems.user', JSON.stringify(user))
  return user
}

function LocationProbe() {
  const location = useLocation()
  return <div data-testid="location">{location.pathname}</div>
}

interface RenderOptions {
  /** Roles of the signed-in user, or `null` to render signed out. Defaults to Owner. */
  roles?: Role[] | null
  branchIds?: string[]
  fullName?: string
  /** The URL the app starts on. */
  path?: string
  /** The route pattern `ui` is mounted at (defaults to `path`); use it for `:id` pages. */
  route?: string
}

export function renderApp(ui: ReactElement, options: RenderOptions = {}): RenderResult & { user: ReturnType<typeof userEvent.setup> } {
  const { roles = ['Owner'], branchIds, fullName, path = '/' } = options
  const route = options.route ?? path

  localStorage.clear()
  if (roles) {
    signIn(roles, { branchIds, fullName })
  }

  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } })

  const result = render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <MemoryRouter initialEntries={[path]}>
          <Routes>
            <Route path={route} element={ui} />
            <Route path="*" element={<LocationProbe />} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </QueryClientProvider>,
  )

  return Object.assign(result, { user: userEvent.setup() })
}

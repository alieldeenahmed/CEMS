import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import { apiClient } from '@/shared/api/client'
import { AUTH_TOKEN_STORAGE_KEY } from './constants'
import { getBranchIdsFromToken, isTokenExpired } from './jwt'
import type { AuthResult, AuthUser, LoginRequest } from './types'

interface AuthContextValue {
  user: AuthUser | null
  isAuthenticated: boolean
  isLoading: boolean
  login: (credentials: LoginRequest) => Promise<void>
  logout: () => void
  hasRole: (...roles: string[]) => boolean
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

function buildUserFromResult(result: AuthResult): AuthUser {
  return {
    userId: result.userId,
    email: result.email,
    fullName: result.fullName,
    roles: result.roles,
    branchIds: getBranchIdsFromToken(result.token),
  }
}

function loadInitialUser(): AuthUser | null {
  const token = localStorage.getItem(AUTH_TOKEN_STORAGE_KEY)
  if (!token || isTokenExpired(token)) {
    localStorage.removeItem(AUTH_TOKEN_STORAGE_KEY)
    return null
  }

  const stored = localStorage.getItem('cems.user')
  if (!stored) return null

  try {
    return JSON.parse(stored) as AuthUser
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(loadInitialUser)
  const [isLoading, setIsLoading] = useState(false)

  const login = useCallback(async (credentials: LoginRequest) => {
    setIsLoading(true)
    try {
      const { data } = await apiClient.post<AuthResult>('/auth/login', credentials)
      const nextUser = buildUserFromResult(data)
      localStorage.setItem(AUTH_TOKEN_STORAGE_KEY, data.token)
      localStorage.setItem('cems.user', JSON.stringify(nextUser))
      setUser(nextUser)
    } finally {
      setIsLoading(false)
    }
  }, [])

  const logout = useCallback(() => {
    localStorage.removeItem(AUTH_TOKEN_STORAGE_KEY)
    localStorage.removeItem('cems.user')
    setUser(null)
  }, [])

  const hasRole = useCallback(
    (...roles: string[]) => !!user && roles.some((role) => user.roles.includes(role as never)),
    [user],
  )

  const value = useMemo<AuthContextValue>(
    () => ({ user, isAuthenticated: !!user, isLoading, login, logout, hasRole }),
    [user, isLoading, login, logout, hasRole],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}

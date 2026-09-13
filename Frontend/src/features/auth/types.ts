import type { Role } from './constants'

export interface AuthUser {
  userId: string
  email: string
  fullName: string
  roles: Role[]
  branchIds: string[]
}

export interface LoginRequest {
  email: string
  password: string
}

export interface AuthResult {
  userId: string
  email: string
  fullName: string
  roles: Role[]
  token: string
  expiresAtUtc: string
}

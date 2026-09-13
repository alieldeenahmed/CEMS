export const AUTH_TOKEN_STORAGE_KEY = 'cems.token'

export const ROLES = {
  Owner: 'Owner',
  BranchManager: 'BranchManager',
  Teacher: 'Teacher',
  FrontDesk: 'FrontDesk',
} as const

export type Role = (typeof ROLES)[keyof typeof ROLES]

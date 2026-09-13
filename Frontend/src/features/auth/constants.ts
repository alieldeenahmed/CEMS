export const AUTH_TOKEN_STORAGE_KEY = 'cems.token'

export const ROLES = {
  Owner: 'Owner',
  BranchManager: 'BranchManager',
  Teacher: 'Teacher',
  FrontDesk: 'FrontDesk',
  Parent: 'Parent',
} as const

export type Role = (typeof ROLES)[keyof typeof ROLES]

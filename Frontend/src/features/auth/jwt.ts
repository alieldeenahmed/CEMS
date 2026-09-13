const ROLE_CLAIM = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/role'

interface DecodedToken {
  sub: string
  email: string
  full_name: string
  branch_id?: string | string[]
  exp: number
  [ROLE_CLAIM]?: string | string[]
}

function base64UrlDecode(input: string): string {
  const padded = input.replace(/-/g, '+').replace(/_/g, '/')
  const base64 = padded.padEnd(padded.length + ((4 - (padded.length % 4)) % 4), '=')
  return decodeURIComponent(
    atob(base64)
      .split('')
      .map((c) => '%' + c.charCodeAt(0).toString(16).padStart(2, '0'))
      .join(''),
  )
}

export function decodeToken(token: string): DecodedToken | null {
  const parts = token.split('.')
  if (parts.length !== 3) return null

  try {
    return JSON.parse(base64UrlDecode(parts[1])) as DecodedToken
  } catch {
    return null
  }
}

function asArray(value: string | string[] | undefined): string[] {
  if (!value) return []
  return Array.isArray(value) ? value : [value]
}

export function getBranchIdsFromToken(token: string): string[] {
  const decoded = decodeToken(token)
  return asArray(decoded?.branch_id)
}

export function isTokenExpired(token: string): boolean {
  const decoded = decodeToken(token)
  if (!decoded) return true
  return decoded.exp * 1000 <= Date.now()
}

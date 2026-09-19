import { describe, expect, it } from 'vitest'
import { fakeToken } from '@/test/harness'
import { decodeToken, getBranchIdsFromToken, isTokenExpired } from './jwt'

describe('decodeToken', () => {
  it('reads the payload of a well-formed token', () => {
    const decoded = decodeToken(fakeToken({ branchIds: ['b1'] }))

    expect(decoded?.email).toBe('test@codecamp.demo')
    expect(decoded?.full_name).toBe('Test User')
  })

  it('returns null for anything that is not a three-part JWT', () => {
    expect(decodeToken('')).toBeNull()
    expect(decodeToken('only.two')).toBeNull()
    expect(decodeToken('a.b.c.d')).toBeNull()
  })

  it('returns null when the payload is not valid JSON', () => {
    expect(decodeToken('header.bm90LWpzb24.signature')).toBeNull()
  })

  it('handles url-safe base64 and non-ASCII names', () => {
    const payload = btoa(unescape(encodeURIComponent(JSON.stringify({ full_name: 'Nourhan Adel — Smouha ✓', exp: 1 }))))
      .replace(/=/g, '')
      .replace(/\+/g, '-')
      .replace(/\//g, '_')

    expect(decodeToken(`h.${payload}.s`)?.full_name).toBe('Nourhan Adel — Smouha ✓')
  })
})

describe('getBranchIdsFromToken', () => {
  it('returns every branch when the claim is repeated (a floating teacher)', () => {
    expect(getBranchIdsFromToken(fakeToken({ branchIds: ['smouha', 'kafr-abdo'] }))).toEqual(['smouha', 'kafr-abdo'])
  })

  it('returns a single branch when the claim is one string', () => {
    expect(getBranchIdsFromToken(fakeToken({ branchIds: ['smouha'] }))).toEqual(['smouha'])
  })

  it('returns an empty list for an Owner, or an unreadable token', () => {
    expect(getBranchIdsFromToken(fakeToken())).toEqual([])
    expect(getBranchIdsFromToken('garbage')).toEqual([])
  })
})

describe('isTokenExpired', () => {
  it('is false for a token that expires in the future', () => {
    expect(isTokenExpired(fakeToken({ expiresInSeconds: 60 }))).toBe(false)
  })

  it('is true once the expiry has passed', () => {
    expect(isTokenExpired(fakeToken({ expiresInSeconds: -1 }))).toBe(true)
  })

  it('treats an unreadable token as expired, so it is never trusted', () => {
    expect(isTokenExpired('garbage')).toBe(true)
  })
})

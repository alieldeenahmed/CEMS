import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { isWithinAttendanceWindow } from './window'

const NOW = new Date('2026-09-15T12:00:00.000Z')

beforeEach(() => {
  vi.useFakeTimers()
  vi.setSystemTime(NOW)
})

afterEach(() => {
  vi.useRealTimers()
})

describe('isWithinAttendanceWindow', () => {
  it('is false for a session that has not started yet', () => {
    const session = { startUtc: '2026-09-15T13:00:00.000Z', endUtc: '2026-09-15T14:00:00.000Z' }
    expect(isWithinAttendanceWindow(session)).toBe(false)
  })

  it('is true while the session is in progress', () => {
    const session = { startUtc: '2026-09-15T11:00:00.000Z', endUtc: '2026-09-15T13:00:00.000Z' }
    expect(isWithinAttendanceWindow(session)).toBe(true)
  })

  it('is true up to exactly 4 hours after the session ends', () => {
    const session = { startUtc: '2026-09-15T06:00:00.000Z', endUtc: '2026-09-15T08:00:00.000Z' } // ends 4h before NOW
    expect(isWithinAttendanceWindow(session)).toBe(true)
  })

  it('is false more than 4 hours after the session ends', () => {
    const session = { startUtc: '2026-09-15T05:00:00.000Z', endUtc: '2026-09-15T07:59:59.000Z' } // ends just over 4h before NOW
    expect(isWithinAttendanceWindow(session)).toBe(false)
  })
})

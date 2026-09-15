import { describe, expect, it } from 'vitest'
import { formatUtcForDisplay, toUtcIso } from './time'

describe('toUtcIso', () => {
  it('appends seconds and a Z suffix to a datetime-local value with no seconds', () => {
    expect(toUtcIso('2026-06-01T10:00')).toBe('2026-06-01T10:00:00.000Z')
  })

  it('leaves an already-complete ISO string untouched', () => {
    expect(toUtcIso('2026-06-01T10:00:00.000Z')).toBe('2026-06-01T10:00:00.000Z')
  })
})

describe('formatUtcForDisplay', () => {
  it('formats a UTC ISO string as "yyyy-MM-dd HH:mm"', () => {
    expect(formatUtcForDisplay('2026-06-01T10:00:00.000Z')).toBe('2026-06-01 10:00')
  })
})

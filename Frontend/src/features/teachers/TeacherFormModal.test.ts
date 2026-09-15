import { describe, expect, it } from 'vitest'
import { teacherSchema } from './TeacherFormModal'

const base = { hireDate: '2026-01-01' }

describe('teacherSchema', () => {
  it('accepts an Hourly rate over 100', () => {
    const result = teacherSchema.safeParse({ ...base, payType: 'Hourly', payRate: 150 })
    expect(result.success).toBe(true)
  })

  it('accepts a Percentage rate at exactly 100', () => {
    const result = teacherSchema.safeParse({ ...base, payType: 'Percentage', payRate: 100 })
    expect(result.success).toBe(true)
  })

  it('rejects a Percentage rate over 100', () => {
    const result = teacherSchema.safeParse({ ...base, payType: 'Percentage', payRate: 150 })
    expect(result.success).toBe(false)
    if (!result.success) {
      expect(result.error.issues[0].path).toEqual(['payRate'])
      expect(result.error.issues[0].message).toContain('cannot exceed 100')
    }
  })

  it('rejects a non-positive pay rate regardless of pay type', () => {
    const result = teacherSchema.safeParse({ ...base, payType: 'Fixed', payRate: 0 })
    expect(result.success).toBe(false)
  })
})

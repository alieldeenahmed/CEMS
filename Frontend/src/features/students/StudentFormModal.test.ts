import { describe, expect, it } from 'vitest'
import { createSchema } from './StudentFormModal'

const base = {
  fullName: 'Sam Student',
  dateOfBirth: '2013-01-01',
  gender: 'Male' as const,
  branchId: 'branch-1',
  relationshipType: 'Guardian' as const,
  isPrimaryContact: false,
}

describe('createSchema (new student, guardian required at creation)', () => {
  it('accepts guardianMode "existing" with an existingGuardianId set', () => {
    const result = createSchema.safeParse({ ...base, guardianMode: 'existing', existingGuardianId: 'g-1' })
    expect(result.success).toBe(true)
  })

  it('rejects guardianMode "existing" with no existingGuardianId', () => {
    const result = createSchema.safeParse({ ...base, guardianMode: 'existing' })
    expect(result.success).toBe(false)
    if (!result.success) {
      expect(result.error.issues.some((i) => i.path.includes('existingGuardianId'))).toBe(true)
    }
  })

  it('accepts guardianMode "new" with all new-guardian fields set', () => {
    const result = createSchema.safeParse({
      ...base,
      guardianMode: 'new',
      newGuardianFullName: 'Pat Parent',
      newGuardianPhone: '0400000000',
      newGuardianEmail: 'pat@example.com',
    })
    expect(result.success).toBe(true)
  })

  it('rejects guardianMode "new" missing the guardian phone, even though it is otherwise optional', () => {
    const result = createSchema.safeParse({
      ...base,
      guardianMode: 'new',
      newGuardianFullName: 'Pat Parent',
      newGuardianEmail: 'pat@example.com',
      // newGuardianPhone omitted
    })
    expect(result.success).toBe(false)
    if (!result.success) {
      expect(result.error.issues.some((i) => i.path.includes('newGuardianPhone'))).toBe(true)
    }
  })

  it('rejects a student with no guardian information supplied at all', () => {
    const result = createSchema.safeParse({ ...base, guardianMode: 'new' })
    expect(result.success).toBe(false)
  })
})

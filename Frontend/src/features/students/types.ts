export type Gender = 'Male' | 'Female'
export type StudentStatus = 'Active' | 'Paused' | 'Graduated'
export type RelationshipType = 'Mother' | 'Father' | 'Guardian'

export interface Student {
  id: string
  fullName: string
  dateOfBirth: string
  gender: Gender
  enrollmentDate: string
  status: StudentStatus
  currentBranchId: string
}

export interface CreateStudentInput {
  fullName: string
  dateOfBirth: string
  gender: Gender
  branchId: string
  existingGuardianId: string | null
  newGuardianFullName: string | null
  newGuardianPhone: string | null
  newGuardianEmail: string | null
  relationshipType: RelationshipType
  isPrimaryContact: boolean
}

export interface UpdateStudentInput {
  fullName: string
  dateOfBirth: string
  gender: Gender
  status: StudentStatus
}

export interface Guardian {
  id: string
  fullName: string
  phone: string
  email: string
}

export interface CreateGuardianInput {
  fullName: string
  phone: string
  email: string
}

export interface LinkGuardianInput {
  guardianId: string
  relationshipType: RelationshipType
  isPrimaryContact: boolean
}

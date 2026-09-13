export interface StaffUser {
  userId: string
  email: string
  fullName: string
  roles: string[]
  branchIds: string[]
  isActive: boolean
}

export interface CreateStaffUserInput {
  email: string
  password: string
  fullName: string
  phoneNumber: string
  role: string
  branchId: string | null
}

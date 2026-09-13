export type PayType = 'Hourly' | 'PerSession'

export const DAYS_OF_WEEK = [
  'Sunday',
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
] as const

export type DayOfWeek = (typeof DAYS_OF_WEEK)[number]

export interface Teacher {
  id: string
  userId: string
  fullName: string
  email: string
  hireDate: string
  payType: PayType
  payRate: number
  branchIds: string[]
}

export interface CreateTeacherInput {
  userId: string
  hireDate: string
  payType: PayType
  payRate: number
}

export interface UpdateTeacherInput {
  hireDate: string
  payType: PayType
  payRate: number
}

export interface TeacherAvailability {
  id: string
  teacherId: string
  branchId: string
  dayOfWeek: DayOfWeek
  startTime: string
  endTime: string
}

export interface AddAvailabilityInput {
  branchId: string
  dayOfWeek: DayOfWeek
  startTime: string
  endTime: string
}

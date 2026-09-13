export type DeliveryMode = 'Group' | 'OneOnOne'
export type EnrollmentStatus = 'Active' | 'Dropped' | 'Waitlisted'

export interface Course {
  id: string
  name: string
  deliveryMode: DeliveryMode
  subjectId: string
  branchId: string
}

export interface CourseInput {
  name: string
  deliveryMode: DeliveryMode
  subjectId: string
  branchId: string
}

export interface UpdateCourseInput {
  name: string
  deliveryMode: DeliveryMode
}

export interface Enrollment {
  id: string
  studentId: string
  courseId: string
  enrollmentDate: string
  status: EnrollmentStatus
  position: number | null
}

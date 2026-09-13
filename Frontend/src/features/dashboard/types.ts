export interface RevenueSummary {
  totalInvoiced: number
  totalCollected: number
  totalOutstanding: number
}

export interface TeacherUtilization {
  teacherId: string
  teacherFullName: string
  scheduledHours: number
  availableHours: number
  utilizationRate: number
}

export interface AttendanceTrends {
  totalRecords: number
  presentCount: number
  absentCount: number
  lateCount: number
  excusedCount: number
  attendanceRate: number
}

export interface EnrollmentFunnel {
  totalStudents: number
  studentsWithAnyEnrollment: number
  studentsWithActiveEnrollment: number
}

export interface DashboardSummary {
  branchId: string | null
  periodStart: string
  periodEnd: string
  revenue: RevenueSummary
  teacherUtilization: TeacherUtilization[]
  attendanceTrends: AttendanceTrends
  enrollmentFunnel: EnrollmentFunnel
}

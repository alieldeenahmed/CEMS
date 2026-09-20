using CEMS.Domain.Attendance;
using CEMS.Domain.Branches;
using CEMS.Domain.Courses;
using CEMS.Domain.Exams;
using CEMS.Domain.Payments;
using CEMS.Domain.Payroll;
using CEMS.Domain.Students;
using CEMS.Domain.Teachers;
using CEMS.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CEMS.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Branch> Branches { get; }
    DbSet<Room> Rooms { get; }
    DbSet<UserBranchAssignment> UserBranchAssignments { get; }
    DbSet<Student> Students { get; }
    DbSet<Guardian> Guardians { get; }
    DbSet<StudentGuardian> StudentGuardians { get; }
    DbSet<StudentBranchHistory> StudentBranchHistories { get; }
    DbSet<Teacher> Teachers { get; }
    DbSet<TeacherBranch> TeacherBranches { get; }
    DbSet<TeacherAvailability> TeacherAvailabilities { get; }
    DbSet<TeacherCourseQualification> TeacherCourseQualifications { get; }
    DbSet<Curriculum> Curricula { get; }
    DbSet<Course> Courses { get; }
    DbSet<CourseEnrollment> CourseEnrollments { get; }
    DbSet<CourseSession> CourseSessions { get; }
    DbSet<SessionAttendance> SessionAttendances { get; }
    DbSet<Exam> Exams { get; }
    DbSet<Grade> Grades { get; }
    DbSet<Package> Packages { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<Payment> Payments { get; }
    DbSet<PayrollRun> PayrollRuns { get; }
    DbSet<PayrollLineItem> PayrollLineItems { get; }
    DbSet<StaffPayrollRun> StaffPayrollRuns { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a database transaction. A handler needs one only when the operation is genuinely atomic
    /// across more than one <see cref="SaveChangesAsync"/> (or across Identity and this context, which
    /// share one connection); a single SaveChangesAsync is already atomic on its own.
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes concurrent "check, then write" sequences on the named resources. Must be called inside
    /// a transaction: the lock is held until that transaction commits or rolls back, so a competing
    /// request re-runs its checks only after this one's write is visible. Keys are taken in a fixed
    /// order, so two requests locking overlapping sets cannot deadlock. On PostgreSQL this is a
    /// transaction-scoped advisory lock; providers without one (the SQLite test database is
    /// single-writer) treat it as a no-op.
    /// </summary>
    Task AcquireLocksAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);
}

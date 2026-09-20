using CEMS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace CEMS.Application.Common.Concurrency;

/// <summary>
/// The names of the things handlers serialize on. Kept in one place so two handlers that must exclude each
/// other (creating a session and substituting a teacher, both of which book a teacher) cannot drift apart
/// by spelling the same lock differently.
/// </summary>
public static class LockKeys
{
    public static string Room(Guid roomId) => $"session-room:{roomId}";
    public static string Teacher(Guid teacherId) => $"session-teacher:{teacherId}";
    public static string CourseEnrollments(Guid courseId) => $"course-enrollments:{courseId}";
    public static string Invoice(Guid invoiceId) => $"invoice:{invoiceId}";
    public static string StudentBranch(Guid studentId) => $"student-branch:{studentId}";
    public static string TeacherPayroll(Guid teacherId) => $"teacher-payroll:{teacherId}";
    public static string StaffPayroll(Guid userId) => $"staff-payroll:{userId}";
    public static string PayrollRun(Guid runId) => $"payroll-run:{runId}";
    public const string OwnerBootstrap = "bootstrap-owner";
}

public static class LockingExtensions
{
    /// <summary>
    /// Opens a transaction and takes the given locks, in that order of steps, so everything the handler reads
    /// afterwards reflects every request that held the lock before it. Commit the returned transaction once the
    /// handler's writes are saved; disposing it without committing rolls everything back and releases the locks.
    /// </summary>
    public static async Task<IDbContextTransaction> BeginLockedTransactionAsync(
        this IApplicationDbContext context, CancellationToken cancellationToken, params string[] lockKeys)
    {
        var transaction = await context.BeginTransactionAsync(cancellationToken);
        try
        {
            await context.AcquireLocksAsync(lockKeys, cancellationToken);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }
}

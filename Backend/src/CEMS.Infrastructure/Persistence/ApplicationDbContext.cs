using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Attendance;
using CEMS.Domain.Branches;
using CEMS.Domain.Courses;
using CEMS.Domain.Exams;
using CEMS.Domain.Payments;
using CEMS.Domain.Payroll;
using CEMS.Domain.Students;
using CEMS.Domain.Teachers;
using CEMS.Domain.Users;
using CEMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql;

namespace CEMS.Infrastructure.Persistence;

/// <summary>Names of the constraints that handlers translate into friendly errors (the exclusion ones cannot be expressed in the EF model).</summary>
public static class SchedulingConstraints
{
    public const string RoomNoOverlap = "ex_course_sessions_room_no_overlap";
    public const string TeacherNoOverlap = "ex_course_sessions_teacher_no_overlap";
    public const string PayrollTeacherNoOverlap = "ex_payroll_runs_teacher_no_period_overlap";
    public const string PayrollStaffNoOverlap = "ex_staff_payroll_runs_user_no_period_overlap";
    public const string EnrollmentLivePerStudentCourse = "ux_course_enrollments_live_per_student_course";
    public const string EnrollmentWaitlistPosition = "ux_course_enrollments_waitlist_position";
}

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<UserBranchAssignment> UserBranchAssignments => Set<UserBranchAssignment>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<StudentGuardian> StudentGuardians => Set<StudentGuardian>();
    public DbSet<StudentBranchHistory> StudentBranchHistories => Set<StudentBranchHistory>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<TeacherBranch> TeacherBranches => Set<TeacherBranch>();
    public DbSet<TeacherAvailability> TeacherAvailabilities => Set<TeacherAvailability>();
    public DbSet<TeacherCourseQualification> TeacherCourseQualifications => Set<TeacherCourseQualification>();
    public DbSet<Curriculum> Curricula => Set<Curriculum>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseEnrollment> CourseEnrollments => Set<CourseEnrollment>();
    public DbSet<CourseSession> CourseSessions => Set<CourseSession>();
    public DbSet<SessionAttendance> SessionAttendances => Set<SessionAttendance>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<PayrollLineItem> PayrollLineItems => Set<PayrollLineItem>();
    public DbSet<StaffPayrollRun> StaffPayrollRuns => Set<StaffPayrollRun>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Database.BeginTransactionAsync(cancellationToken);

    public async Task AcquireLocksAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        if (Database.CurrentTransaction is null)
        {
            // Outside a transaction an advisory xact lock is released as soon as its own statement ends,
            // which would silently protect nothing -- so refuse rather than pretend.
            throw new InvalidOperationException("AcquireLocksAsync must be called inside a transaction.");
        }

        if (!Database.IsNpgsql())
        {
            return;
        }

        foreach (var key in keys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            await Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
        }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation } violation)
        {
            // The database-level backstop for double-booking (see the AddSchedulingOverlapConstraints
            // migration) fired: report it as the same scheduling conflict the handlers raise.
            throw violation.ConstraintName switch
            {
                SchedulingConstraints.RoomNoOverlap => new SchedulingConflictException(new[] { "The room is already booked for an overlapping time slot." }, ex),
                SchedulingConstraints.TeacherNoOverlap => new SchedulingConflictException(new[] { "The teacher is already booked for an overlapping time slot." }, ex),
                SchedulingConstraints.PayrollTeacherNoOverlap or SchedulingConstraints.PayrollStaffNoOverlap =>
                    new BadRequestException(new[] { "A payroll run already exists covering an overlapping period." }),
                _ => new BadRequestException(new[] { "This overlaps an existing record." })
            };
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } violation)
        {
            // Reached only when two requests raced past a handler's own check; report what the user needs to
            // know rather than surfacing a raw constraint error as a 500.
            throw new BadRequestException(new[]
            {
                violation.ConstraintName switch
                {
                    SchedulingConstraints.EnrollmentLivePerStudentCourse => "This student is already enrolled or waitlisted for this course.",
                    SchedulingConstraints.EnrollmentWaitlistPosition => "The waitlist changed at the same moment; please try again.",
                    _ => "This record was created at the same moment by another request; please refresh and check."
                }
            });
        }
    }

    internal static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Npgsql requires DateTime.Kind == Utc for "timestamp with time zone" columns, but values
        // arriving from JSON deserialization (or anywhere else) may not be. All DateTime values in this
        // model represent UTC instants, so normalise them at the boundary: a Local value (which is what
        // System.Text.Json produces for "2030-01-07T12:00:00+02:00") is converted to the instant it
        // denotes, and an Unspecified one (no zone in the JSON at all) is taken to already be UTC.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => ToUtc(v),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(utcConverter);
                }
            }
        }

        builder.Entity<IdentityRole<Guid>>().HasData(
            new IdentityRole<Guid>
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = RoleNames.Owner,
                NormalizedName = RoleNames.Owner.ToUpperInvariant(),
                ConcurrencyStamp = "11111111-1111-1111-1111-111111111111"
            },
            new IdentityRole<Guid>
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = RoleNames.BranchManager,
                NormalizedName = RoleNames.BranchManager.ToUpperInvariant(),
                ConcurrencyStamp = "22222222-2222-2222-2222-222222222222"
            },
            new IdentityRole<Guid>
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = RoleNames.Teacher,
                NormalizedName = RoleNames.Teacher.ToUpperInvariant(),
                ConcurrencyStamp = "33333333-3333-3333-3333-333333333333"
            },
            new IdentityRole<Guid>
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Name = RoleNames.FrontDesk,
                NormalizedName = RoleNames.FrontDesk.ToUpperInvariant(),
                ConcurrencyStamp = "44444444-4444-4444-4444-444444444444"
            }
        );
    }
}

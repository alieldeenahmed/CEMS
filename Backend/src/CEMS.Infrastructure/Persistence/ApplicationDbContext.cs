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
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CEMS.Infrastructure.Persistence;

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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Npgsql requires DateTime.Kind == Utc for "timestamp with time zone" columns, but values
        // arriving from JSON deserialization (or anywhere else) may have Kind == Unspecified. All
        // DateTime values in this model represent UTC instants, so force that Kind consistently
        // rather than relying on every caller to get it right.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
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

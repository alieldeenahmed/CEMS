using CEMS.Domain.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEMS.Infrastructure.Persistence.Configurations;

public class CourseEnrollmentConfiguration : IEntityTypeConfiguration<CourseEnrollment>
{
    public void Configure(EntityTypeBuilder<CourseEnrollment> builder)
    {
        // A student holds at most one live (Active or Waitlisted) enrollment per course; a Dropped one may sit
        // alongside it, which is how re-enrolling works. Status 1 is CourseEnrollmentStatus.Dropped.
        builder.HasIndex(e => new { e.StudentId, e.CourseId })
            .IsUnique()
            .HasFilter("\"Status\" <> 1")
            .HasDatabaseName("ux_course_enrollments_live_per_student_course");

        // Waitlist positions are unique within a course, so "who is next" is never ambiguous.
        builder.HasIndex(e => new { e.CourseId, e.Position })
            .IsUnique()
            .HasFilter("\"Position\" IS NOT NULL")
            .HasDatabaseName("ux_course_enrollments_waitlist_position");

        builder.HasOne(e => e.Student)
            .WithMany()
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Course)
            .WithMany(c => c.Enrollments)
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

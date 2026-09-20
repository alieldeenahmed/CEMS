using CEMS.Domain.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEMS.Infrastructure.Persistence.Configurations;

public class CourseSessionConfiguration : IEntityTypeConfiguration<CourseSession>
{
    public void Configure(EntityTypeBuilder<CourseSession> builder)
    {
        builder.Property(s => s.OverrideReason).HasMaxLength(500);

        // Also a precondition of the overlap exclusion constraint (see the AddSchedulingOverlapConstraints
        // migration), whose tstzrange(start, end) raises an error for an inverted range.
        builder.ToTable(t => t.HasCheckConstraint("ck_course_sessions_end_after_start", "\"EndUtc\" > \"StartUtc\""));

        builder.HasOne(s => s.Course)
            .WithMany()
            .HasForeignKey(s => s.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Room)
            .WithMany()
            .HasForeignKey(s => s.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Teacher)
            .WithMany()
            .HasForeignKey(s => s.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.RescheduledToSession)
            .WithMany()
            .HasForeignKey(s => s.RescheduledToSessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

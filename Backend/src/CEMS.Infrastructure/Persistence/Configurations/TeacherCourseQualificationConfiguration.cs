using CEMS.Domain.Teachers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEMS.Infrastructure.Persistence.Configurations;

public class TeacherCourseQualificationConfiguration : IEntityTypeConfiguration<TeacherCourseQualification>
{
    public void Configure(EntityTypeBuilder<TeacherCourseQualification> builder)
    {
        builder.HasKey(q => new { q.TeacherId, q.CourseId });

        builder.HasOne(q => q.Teacher)
            .WithMany(t => t.Qualifications)
            .HasForeignKey(q => q.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(q => q.Course)
            .WithMany()
            .HasForeignKey(q => q.CourseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using CEMS.Domain.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEMS.Infrastructure.Persistence.Configurations;

public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);

        builder.HasOne(c => c.Subject)
            .WithMany(s => s.Courses)
            .HasForeignKey(c => c.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Branch)
            .WithMany()
            .HasForeignKey(c => c.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

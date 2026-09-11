using CEMS.Domain.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEMS.Infrastructure.Persistence.Configurations;

public class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.Property(s => s.Name).IsRequired().HasMaxLength(100);

        builder.HasOne(s => s.Curriculum)
            .WithMany(c => c.Subjects)
            .HasForeignKey(s => s.CurriculumId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using CEMS.Domain.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEMS.Infrastructure.Persistence.Configurations;

public class CurriculumConfiguration : IEntityTypeConfiguration<Curriculum>
{
    public void Configure(EntityTypeBuilder<Curriculum> builder)
    {
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Description).HasMaxLength(500);
    }
}

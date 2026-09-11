using CEMS.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEMS.Infrastructure.Persistence.Configurations;

public class GuardianConfiguration : IEntityTypeConfiguration<Guardian>
{
    public void Configure(EntityTypeBuilder<Guardian> builder)
    {
        builder.Property(g => g.FullName).IsRequired().HasMaxLength(200);
        builder.Property(g => g.Phone).IsRequired().HasMaxLength(30);
        builder.Property(g => g.Email).IsRequired().HasMaxLength(256);
    }
}

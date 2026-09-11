using CEMS.Domain.Branches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEMS.Infrastructure.Persistence.Configurations;

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.Property(b => b.Name).IsRequired().HasMaxLength(200);
        builder.Property(b => b.Address).HasMaxLength(500);
        builder.Property(b => b.Phone).HasMaxLength(30);
    }
}

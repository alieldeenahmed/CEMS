using CEMS.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEMS.Infrastructure.Persistence.Configurations;

public class PackageConfiguration : IEntityTypeConfiguration<Package>
{
    public void Configure(EntityTypeBuilder<Package> builder)
    {
        builder.Property(p => p.Price).HasColumnType("numeric(10,2)");
        builder.ToTable(t => t.HasCheckConstraint("ck_packages_price_and_sessions_positive", "\"Price\" > 0 AND \"SessionCount\" > 0"));

        builder.HasOne(p => p.Course)
            .WithMany()
            .HasForeignKey(p => p.CourseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

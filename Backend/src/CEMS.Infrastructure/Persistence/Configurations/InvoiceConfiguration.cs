using CEMS.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEMS.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.Property(i => i.Amount).HasColumnType("numeric(10,2)");

        builder.HasOne(i => i.Student)
            .WithMany()
            .HasForeignKey(i => i.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Package)
            .WithMany()
            .HasForeignKey(i => i.PackageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

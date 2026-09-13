using CEMS.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEMS.Infrastructure.Persistence.Configurations;

public class PayrollLineItemConfiguration : IEntityTypeConfiguration<PayrollLineItem>
{
    public void Configure(EntityTypeBuilder<PayrollLineItem> builder)
    {
        builder.Property(li => li.Amount).HasColumnType("numeric(10,2)");

        builder.HasOne(li => li.PayrollRun)
            .WithMany(r => r.LineItems)
            .HasForeignKey(li => li.PayrollRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(li => li.CourseSession)
            .WithMany()
            .HasForeignKey(li => li.CourseSessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

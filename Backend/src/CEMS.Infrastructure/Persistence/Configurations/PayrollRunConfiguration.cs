using CEMS.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEMS.Infrastructure.Persistence.Configurations;

public class PayrollRunConfiguration : IEntityTypeConfiguration<PayrollRun>
{
    public void Configure(EntityTypeBuilder<PayrollRun> builder)
    {
        builder.Property(r => r.TotalAmount).HasColumnType("numeric(10,2)");
        builder.ToTable(t => t.HasCheckConstraint("ck_payroll_runs_period_ordered", "\"PeriodEnd\" >= \"PeriodStart\""));

        builder.HasOne(r => r.Teacher)
            .WithMany()
            .HasForeignKey(r => r.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

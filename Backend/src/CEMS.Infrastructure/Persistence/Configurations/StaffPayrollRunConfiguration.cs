using CEMS.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEMS.Infrastructure.Persistence.Configurations;

public class StaffPayrollRunConfiguration : IEntityTypeConfiguration<StaffPayrollRun>
{
    public void Configure(EntityTypeBuilder<StaffPayrollRun> builder)
    {
        builder.Property(r => r.Amount).HasColumnType("numeric(10,2)");
        builder.ToTable(t => t.HasCheckConstraint("ck_staff_payroll_runs_valid", "\"Amount\" > 0 AND \"PeriodEnd\" >= \"PeriodStart\""));
    }
}

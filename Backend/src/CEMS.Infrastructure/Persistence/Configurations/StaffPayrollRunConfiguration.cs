using CEMS.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEMS.Infrastructure.Persistence.Configurations;

public class StaffPayrollRunConfiguration : IEntityTypeConfiguration<StaffPayrollRun>
{
    public void Configure(EntityTypeBuilder<StaffPayrollRun> builder)
    {
        builder.Property(r => r.Amount).HasColumnType("numeric(10,2)");
    }
}

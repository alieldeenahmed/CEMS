using CEMS.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEMS.Infrastructure.Persistence.Configurations;

public class StudentBranchHistoryConfiguration : IEntityTypeConfiguration<StudentBranchHistory>
{
    public void Configure(EntityTypeBuilder<StudentBranchHistory> builder)
    {
        builder.Property(h => h.Reason).HasMaxLength(500);

        builder.HasOne(h => h.Student)
            .WithMany()
            .HasForeignKey(h => h.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.FromBranch)
            .WithMany()
            .HasForeignKey(h => h.FromBranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.ToBranch)
            .WithMany()
            .HasForeignKey(h => h.ToBranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

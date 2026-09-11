using CEMS.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEMS.Infrastructure.Persistence.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.Property(s => s.FullName).IsRequired().HasMaxLength(200);

        builder.HasOne(s => s.CurrentBranch)
            .WithMany()
            .HasForeignKey(s => s.CurrentBranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

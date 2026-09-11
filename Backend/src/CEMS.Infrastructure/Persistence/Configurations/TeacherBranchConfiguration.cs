using CEMS.Domain.Teachers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEMS.Infrastructure.Persistence.Configurations;

public class TeacherBranchConfiguration : IEntityTypeConfiguration<TeacherBranch>
{
    public void Configure(EntityTypeBuilder<TeacherBranch> builder)
    {
        builder.HasKey(tb => new { tb.TeacherId, tb.BranchId });

        builder.HasOne(tb => tb.Teacher)
            .WithMany(t => t.TeacherBranches)
            .HasForeignKey(tb => tb.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tb => tb.Branch)
            .WithMany()
            .HasForeignKey(tb => tb.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using CEMS.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEMS.Infrastructure.Persistence.Configurations;

public class StudentGuardianConfiguration : IEntityTypeConfiguration<StudentGuardian>
{
    public void Configure(EntityTypeBuilder<StudentGuardian> builder)
    {
        builder.HasKey(sg => new { sg.StudentId, sg.GuardianId });

        builder.HasOne(sg => sg.Student)
            .WithMany(s => s.StudentGuardians)
            .HasForeignKey(sg => sg.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sg => sg.Guardian)
            .WithMany(g => g.StudentGuardians)
            .HasForeignKey(sg => sg.GuardianId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

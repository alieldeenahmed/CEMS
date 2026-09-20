using CEMS.Domain.Exams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEMS.Infrastructure.Persistence.Configurations;

public class GradeConfiguration : IEntityTypeConfiguration<Grade>
{
    public void Configure(EntityTypeBuilder<Grade> builder)
    {
        builder.Property(g => g.Score).HasColumnType("numeric(6,2)");
        builder.ToTable(t => t.HasCheckConstraint("ck_grades_score_not_negative", "\"Score\" >= 0"));
        builder.Property(g => g.Comments).HasMaxLength(1000);

        builder.HasIndex(g => new { g.ExamId, g.StudentId }).IsUnique();

        builder.HasOne(g => g.Exam)
            .WithMany(e => e.Grades)
            .HasForeignKey(g => g.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(g => g.Student)
            .WithMany()
            .HasForeignKey(g => g.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

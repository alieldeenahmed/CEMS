using CEMS.Domain.Students;

namespace CEMS.Domain.Exams;

public class Grade
{
    public Guid Id { get; set; }

    public Guid ExamId { get; set; }
    public Exam Exam { get; set; } = null!;

    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public decimal Score { get; set; }
    public string? Comments { get; set; }
    public DateTime? GradedAtUtc { get; set; }
    public Guid? GradedByUserId { get; set; }
}

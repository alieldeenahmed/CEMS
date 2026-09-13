namespace CEMS.Application.Exams;

public record GradeDto(
    Guid? Id,
    Guid ExamId,
    string ExamName,
    decimal ExamMaxScore,
    Guid StudentId,
    string StudentFullName,
    decimal? Score,
    string? Comments,
    DateTime? GradedAtUtc,
    Guid? GradedByUserId);

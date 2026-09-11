namespace CEMS.Application.Exams;

public record GradeDto(
    Guid? Id,
    Guid ExamId,
    Guid StudentId,
    decimal? Score,
    string? Comments,
    DateTime? GradedAtUtc,
    Guid? GradedByUserId);

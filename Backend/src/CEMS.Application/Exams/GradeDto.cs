namespace CEMS.Application.Exams;

public record GradeDto(
    Guid? Id,
    Guid ExamId,
    string ExamName,
    decimal ExamMaxScore,
    DateOnly ExamDate,
    Guid CourseId,
    string CourseName,
    Guid StudentId,
    string StudentFullName,
    decimal? Score,
    string? Comments,
    DateTime? GradedAtUtc,
    Guid? GradedByUserId);

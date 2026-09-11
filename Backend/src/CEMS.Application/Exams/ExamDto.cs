namespace CEMS.Application.Exams;

public record ExamDto(Guid Id, Guid CourseId, string Name, decimal MaxScore, DateOnly ExamDate);

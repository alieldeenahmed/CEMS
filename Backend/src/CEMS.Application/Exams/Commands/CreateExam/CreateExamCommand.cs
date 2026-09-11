using MediatR;

namespace CEMS.Application.Exams.Commands.CreateExam;

public record CreateExamCommand(Guid CourseId, string Name, decimal MaxScore, DateOnly ExamDate) : IRequest<ExamDto>;

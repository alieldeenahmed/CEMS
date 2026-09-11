using MediatR;

namespace CEMS.Application.Exams.Commands.UpdateExam;

public record UpdateExamCommand(Guid Id, string Name, decimal MaxScore, DateOnly ExamDate) : IRequest<ExamDto>;

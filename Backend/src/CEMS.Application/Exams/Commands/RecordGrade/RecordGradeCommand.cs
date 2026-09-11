using MediatR;

namespace CEMS.Application.Exams.Commands.RecordGrade;

public record RecordGradeCommand(Guid ExamId, Guid StudentId, decimal Score, string? Comments) : IRequest<GradeDto>;

using MediatR;

namespace CEMS.Application.Exams.Queries.GetExamById;

public record GetExamByIdQuery(Guid Id) : IRequest<ExamDto>;

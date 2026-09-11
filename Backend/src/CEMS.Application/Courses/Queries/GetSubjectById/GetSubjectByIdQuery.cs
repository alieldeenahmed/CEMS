using MediatR;

namespace CEMS.Application.Courses.Queries.GetSubjectById;

public record GetSubjectByIdQuery(Guid Id) : IRequest<SubjectDto>;

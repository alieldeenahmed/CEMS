using MediatR;

namespace CEMS.Application.Scheduling.Queries.GetSessionById;

public record GetSessionByIdQuery(Guid Id) : IRequest<CourseSessionDto>;

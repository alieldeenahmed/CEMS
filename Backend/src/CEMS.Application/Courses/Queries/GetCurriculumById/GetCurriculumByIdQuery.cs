using MediatR;

namespace CEMS.Application.Courses.Queries.GetCurriculumById;

public record GetCurriculumByIdQuery(Guid Id) : IRequest<CurriculumDto>;

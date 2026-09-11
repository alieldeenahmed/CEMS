using MediatR;

namespace CEMS.Application.Courses.Queries.GetCurricula;

public record GetCurriculaQuery : IRequest<List<CurriculumDto>>;

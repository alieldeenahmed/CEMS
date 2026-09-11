using MediatR;

namespace CEMS.Application.Courses.Commands.UpdateCurriculum;

public record UpdateCurriculumCommand(Guid Id, string Name, string Description) : IRequest<CurriculumDto>;

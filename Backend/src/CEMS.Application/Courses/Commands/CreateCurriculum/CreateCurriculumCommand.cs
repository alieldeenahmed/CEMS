using MediatR;

namespace CEMS.Application.Courses.Commands.CreateCurriculum;

public record CreateCurriculumCommand(string Name, string Description) : IRequest<CurriculumDto>;

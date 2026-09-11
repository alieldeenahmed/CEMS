using MediatR;

namespace CEMS.Application.Courses.Commands.DeleteCurriculum;

public record DeleteCurriculumCommand(Guid Id) : IRequest;

using MediatR;

namespace CEMS.Application.Courses.Commands.UpdateSubject;

public record UpdateSubjectCommand(Guid Id, string Name) : IRequest<SubjectDto>;

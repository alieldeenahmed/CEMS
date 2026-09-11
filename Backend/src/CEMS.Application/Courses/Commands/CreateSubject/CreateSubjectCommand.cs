using MediatR;

namespace CEMS.Application.Courses.Commands.CreateSubject;

public record CreateSubjectCommand(string Name, Guid CurriculumId) : IRequest<SubjectDto>;

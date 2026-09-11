using MediatR;

namespace CEMS.Application.Courses.Commands.DeleteSubject;

public record DeleteSubjectCommand(Guid Id) : IRequest;

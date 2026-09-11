using MediatR;

namespace CEMS.Application.Students.Commands.DeleteStudent;

public record DeleteStudentCommand(Guid Id) : IRequest;

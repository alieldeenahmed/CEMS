using MediatR;

namespace CEMS.Application.Teachers.Commands.DeleteTeacher;

public record DeleteTeacherCommand(Guid Id) : IRequest;

using CEMS.Domain.Teachers;
using MediatR;

namespace CEMS.Application.Teachers.Commands.UpdateTeacher;

public record UpdateTeacherCommand(Guid Id, DateOnly HireDate, PayType PayType, decimal PayRate) : IRequest<TeacherDto>;

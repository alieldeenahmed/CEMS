using CEMS.Domain.Teachers;
using MediatR;

namespace CEMS.Application.Teachers.Commands.CreateTeacher;

public record CreateTeacherCommand(Guid UserId, DateOnly HireDate, PayType PayType, decimal PayRate) : IRequest<TeacherDto>;

using MediatR;

namespace CEMS.Application.Teachers.Queries.GetTeacherById;

public record GetTeacherByIdQuery(Guid Id) : IRequest<TeacherDto>;

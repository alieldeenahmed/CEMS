using MediatR;

namespace CEMS.Application.Teachers.Queries.GetTeachers;

public record GetTeachersQuery : IRequest<List<TeacherDto>>;

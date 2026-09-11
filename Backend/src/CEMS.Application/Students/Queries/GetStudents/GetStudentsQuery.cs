using MediatR;

namespace CEMS.Application.Students.Queries.GetStudents;

public record GetStudentsQuery : IRequest<List<StudentDto>>;

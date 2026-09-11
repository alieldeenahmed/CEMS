using MediatR;

namespace CEMS.Application.Students.Queries.GetStudentById;

public record GetStudentByIdQuery(Guid Id) : IRequest<StudentDto>;

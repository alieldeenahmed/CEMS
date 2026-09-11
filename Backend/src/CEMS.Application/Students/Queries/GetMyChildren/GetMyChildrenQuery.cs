using MediatR;

namespace CEMS.Application.Students.Queries.GetMyChildren;

public record GetMyChildrenQuery : IRequest<List<StudentDto>>;

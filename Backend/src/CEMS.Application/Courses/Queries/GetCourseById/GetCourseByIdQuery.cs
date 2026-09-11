using MediatR;

namespace CEMS.Application.Courses.Queries.GetCourseById;

public record GetCourseByIdQuery(Guid Id) : IRequest<CourseDto>;

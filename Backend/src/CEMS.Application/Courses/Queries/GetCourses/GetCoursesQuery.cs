using MediatR;

namespace CEMS.Application.Courses.Queries.GetCourses;

public record GetCoursesQuery : IRequest<List<CourseDto>>;

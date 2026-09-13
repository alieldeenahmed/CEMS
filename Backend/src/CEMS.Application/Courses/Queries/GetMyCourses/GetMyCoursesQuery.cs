using MediatR;

namespace CEMS.Application.Courses.Queries.GetMyCourses;

public record GetMyCoursesQuery : IRequest<List<CourseDto>>;

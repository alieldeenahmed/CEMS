using MediatR;

namespace CEMS.Application.Courses.Queries.GetEnrollmentsForCourse;

public record GetEnrollmentsForCourseQuery(Guid CourseId) : IRequest<List<CourseEnrollmentDto>>;

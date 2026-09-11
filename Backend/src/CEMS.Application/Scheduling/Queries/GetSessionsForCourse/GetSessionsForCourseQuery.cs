using MediatR;

namespace CEMS.Application.Scheduling.Queries.GetSessionsForCourse;

public record GetSessionsForCourseQuery(Guid CourseId) : IRequest<List<CourseSessionDto>>;

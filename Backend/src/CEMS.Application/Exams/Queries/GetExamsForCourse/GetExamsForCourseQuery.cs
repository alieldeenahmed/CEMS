using MediatR;

namespace CEMS.Application.Exams.Queries.GetExamsForCourse;

public record GetExamsForCourseQuery(Guid CourseId) : IRequest<List<ExamDto>>;

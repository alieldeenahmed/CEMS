using MediatR;

namespace CEMS.Application.Courses.Queries.GetEnrollmentsForStudent;

public record GetEnrollmentsForStudentQuery(Guid StudentId) : IRequest<List<CourseEnrollmentDto>>;

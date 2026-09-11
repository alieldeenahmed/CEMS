using MediatR;

namespace CEMS.Application.Courses.Commands.EnrollStudent;

public record EnrollStudentCommand(Guid StudentId, Guid CourseId) : IRequest<CourseEnrollmentDto>;

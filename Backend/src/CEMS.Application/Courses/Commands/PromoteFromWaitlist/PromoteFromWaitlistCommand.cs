using MediatR;

namespace CEMS.Application.Courses.Commands.PromoteFromWaitlist;

public record PromoteFromWaitlistCommand(Guid EnrollmentId) : IRequest<CourseEnrollmentDto>;

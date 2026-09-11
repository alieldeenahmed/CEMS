using MediatR;

namespace CEMS.Application.Courses.Commands.DropEnrollment;

public record DropEnrollmentCommand(Guid Id) : IRequest;

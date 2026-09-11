using MediatR;

namespace CEMS.Application.Scheduling.Commands.CancelSession;

public record CancelSessionCommand(Guid Id, Guid? RescheduledToSessionId) : IRequest<CourseSessionDto>;

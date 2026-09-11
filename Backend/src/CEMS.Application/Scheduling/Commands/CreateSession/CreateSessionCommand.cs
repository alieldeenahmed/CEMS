using MediatR;

namespace CEMS.Application.Scheduling.Commands.CreateSession;

public record CreateSessionCommand(
    Guid CourseId,
    Guid RoomId,
    Guid TeacherId,
    DateTime StartUtc,
    DateTime EndUtc,
    bool Override,
    string? OverrideReason) : IRequest<CourseSessionDto>;

using MediatR;

namespace CEMS.Application.Students.Commands.UnlinkGuardian;

public record UnlinkGuardianCommand(Guid StudentId, Guid GuardianId) : IRequest;

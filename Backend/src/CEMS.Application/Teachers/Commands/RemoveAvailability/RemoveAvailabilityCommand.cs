using MediatR;

namespace CEMS.Application.Teachers.Commands.RemoveAvailability;

public record RemoveAvailabilityCommand(Guid Id) : IRequest;

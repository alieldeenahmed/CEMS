using MediatR;

namespace CEMS.Application.Payments.Commands.DeletePackage;

public record DeletePackageCommand(Guid Id) : IRequest;

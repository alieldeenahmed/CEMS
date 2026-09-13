using MediatR;

namespace CEMS.Application.Payments.Commands.UpdatePackage;

public record UpdatePackageCommand(Guid Id, int SessionCount, decimal Price) : IRequest<PackageDto>;

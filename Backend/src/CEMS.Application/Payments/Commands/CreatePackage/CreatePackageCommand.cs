using MediatR;

namespace CEMS.Application.Payments.Commands.CreatePackage;

public record CreatePackageCommand(Guid CourseId, int SessionCount, decimal Price) : IRequest<PackageDto>;

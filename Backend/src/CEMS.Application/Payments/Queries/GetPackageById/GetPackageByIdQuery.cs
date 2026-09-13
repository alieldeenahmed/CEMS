using MediatR;

namespace CEMS.Application.Payments.Queries.GetPackageById;

public record GetPackageByIdQuery(Guid Id) : IRequest<PackageDto>;

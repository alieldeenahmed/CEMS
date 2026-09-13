using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payments.Queries.GetPackageById;

public class GetPackageByIdQueryHandler : IRequestHandler<GetPackageByIdQuery, PackageDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetPackageByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PackageDto> Handle(GetPackageByIdQuery request, CancellationToken cancellationToken)
    {
        var package = await _context.Packages
            .Include(p => p.Course)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Package), request.Id);

        if (!_currentUser.HasAccessToBranch(package.Course.BranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this package.");
        }

        return new PackageDto(package.Id, package.CourseId, package.SessionCount, package.Price);
    }
}

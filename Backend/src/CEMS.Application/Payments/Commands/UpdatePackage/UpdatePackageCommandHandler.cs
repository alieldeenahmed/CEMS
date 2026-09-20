using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payments.Commands.UpdatePackage;

public class UpdatePackageCommandHandler : IRequestHandler<UpdatePackageCommand, PackageDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdatePackageCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PackageDto> Handle(UpdatePackageCommand request, CancellationToken cancellationToken)
    {
        var package = await _context.Packages
            .Include(p => p.Course)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Package), request.Id);

        _currentUser.EnsureAccessToBranch(package.Course.BranchId);

        package.SessionCount = request.SessionCount;
        package.Price = request.Price;

        await _context.SaveChangesAsync(cancellationToken);

        return new PackageDto(package.Id, package.CourseId, package.SessionCount, package.Price);
    }
}

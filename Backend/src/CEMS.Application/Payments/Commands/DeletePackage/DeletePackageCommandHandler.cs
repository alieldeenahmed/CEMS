using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payments.Commands.DeletePackage;

public class DeletePackageCommandHandler : IRequestHandler<DeletePackageCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeletePackageCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(DeletePackageCommand request, CancellationToken cancellationToken)
    {
        var package = await _context.Packages
            .Include(p => p.Course)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Package), request.Id);

        _currentUser.EnsureAccessToBranch(package.Course.BranchId);

        _context.Packages.Remove(package);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new BadRequestException(new[] { "Cannot delete a package that has invoices issued against it." });
        }
    }
}

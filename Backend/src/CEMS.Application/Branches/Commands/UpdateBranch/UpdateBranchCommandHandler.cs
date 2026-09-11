using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Branches;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Branches.Commands.UpdateBranch;

public class UpdateBranchCommandHandler : IRequestHandler<UpdateBranchCommand, BranchDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateBranchCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BranchDto> Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Branch), request.Id);

        branch.Name = request.Name;
        branch.Address = request.Address;
        branch.Phone = request.Phone;
        branch.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        return new BranchDto(branch.Id, branch.Name, branch.Address, branch.Phone, branch.IsActive);
    }
}

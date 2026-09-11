using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Branches;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Branches.Queries.GetBranchById;

public class GetBranchByIdQueryHandler : IRequestHandler<GetBranchByIdQuery, BranchDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetBranchByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<BranchDto> Handle(GetBranchByIdQuery request, CancellationToken cancellationToken)
    {
        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Branch), request.Id);

        if (!_currentUser.HasAccessToBranch(branch.Id))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }

        return new BranchDto(branch.Id, branch.Name, branch.Address, branch.Phone, branch.IsActive);
    }
}

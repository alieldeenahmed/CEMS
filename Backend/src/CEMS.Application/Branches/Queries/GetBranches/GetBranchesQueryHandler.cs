using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Branches.Queries.GetBranches;

public class GetBranchesQueryHandler : IRequestHandler<GetBranchesQuery, List<BranchDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetBranchesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<BranchDto>> Handle(GetBranchesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Branches.AsQueryable();

        if (!_currentUser.IsInRole(RoleNames.Owner))
        {
            var branchIds = _currentUser.BranchIds;
            query = query.Where(b => branchIds.Contains(b.Id));
        }

        return await query
            .OrderBy(b => b.Name)
            .Select(b => new BranchDto(b.Id, b.Name, b.Address, b.Phone, b.IsActive))
            .ToListAsync(cancellationToken);
    }
}

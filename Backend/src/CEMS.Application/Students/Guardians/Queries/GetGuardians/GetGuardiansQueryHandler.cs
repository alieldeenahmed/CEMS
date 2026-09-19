using CEMS.Application.Common.Interfaces;
using CEMS.Application.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Students.Guardians.Queries.GetGuardians;

public class GetGuardiansQueryHandler : IRequestHandler<GetGuardiansQuery, List<GuardianDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetGuardiansQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<GuardianDto>> Handle(GetGuardiansQuery request, CancellationToken cancellationToken)
    {
        return await _context.Guardians
            .VisibleTo(_currentUser)
            .OrderBy(g => g.FullName)
            .Select(g => new GuardianDto(g.Id, g.FullName, g.Phone, g.Email))
            .ToListAsync(cancellationToken);
    }
}

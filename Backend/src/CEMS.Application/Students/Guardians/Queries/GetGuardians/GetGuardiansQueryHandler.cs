using CEMS.Application.Common.Interfaces;
using CEMS.Application.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Students.Guardians.Queries.GetGuardians;

public class GetGuardiansQueryHandler : IRequestHandler<GetGuardiansQuery, List<GuardianDto>>
{
    private readonly IApplicationDbContext _context;

    public GetGuardiansQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<GuardianDto>> Handle(GetGuardiansQuery request, CancellationToken cancellationToken)
    {
        return await _context.Guardians
            .OrderBy(g => g.FullName)
            .Select(g => new GuardianDto(g.Id, g.FullName, g.Phone, g.Email, g.UserId))
            .ToListAsync(cancellationToken);
    }
}

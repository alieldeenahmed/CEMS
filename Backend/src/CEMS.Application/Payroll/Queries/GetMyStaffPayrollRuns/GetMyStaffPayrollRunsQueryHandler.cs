using CEMS.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payroll.Queries.GetMyStaffPayrollRuns;

public class GetMyStaffPayrollRunsQueryHandler : IRequestHandler<GetMyStaffPayrollRunsQuery, List<StaffPayrollRunDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyStaffPayrollRunsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<StaffPayrollRunDto>> Handle(GetMyStaffPayrollRunsQuery request, CancellationToken cancellationToken)
    {
        return await _context.StaffPayrollRuns
            .Where(r => r.UserId == _currentUser.UserId)
            .OrderByDescending(r => r.PeriodStart)
            .Select(r => new StaffPayrollRunDto(r.Id, r.UserId, r.PeriodStart, r.PeriodEnd, r.Amount, r.Status))
            .ToListAsync(cancellationToken);
    }
}

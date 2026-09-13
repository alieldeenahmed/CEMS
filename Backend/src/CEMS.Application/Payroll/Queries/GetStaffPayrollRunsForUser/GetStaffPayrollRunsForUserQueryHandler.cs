using CEMS.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payroll.Queries.GetStaffPayrollRunsForUser;

public class GetStaffPayrollRunsForUserQueryHandler : IRequestHandler<GetStaffPayrollRunsForUserQuery, List<StaffPayrollRunDto>>
{
    private readonly IApplicationDbContext _context;

    public GetStaffPayrollRunsForUserQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<StaffPayrollRunDto>> Handle(GetStaffPayrollRunsForUserQuery request, CancellationToken cancellationToken)
    {
        return await _context.StaffPayrollRuns
            .Where(r => r.UserId == request.UserId)
            .OrderByDescending(r => r.PeriodStart)
            .Select(r => new StaffPayrollRunDto(r.Id, r.UserId, r.PeriodStart, r.PeriodEnd, r.Amount, r.Status))
            .ToListAsync(cancellationToken);
    }
}

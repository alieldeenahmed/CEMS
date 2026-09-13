using CEMS.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payroll.Queries.GetMyPayrollRuns;

public class GetMyPayrollRunsQueryHandler : IRequestHandler<GetMyPayrollRunsQuery, List<PayrollRunDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyPayrollRunsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<PayrollRunDto>> Handle(GetMyPayrollRunsQuery request, CancellationToken cancellationToken)
    {
        return await _context.PayrollRuns
            .Where(r => r.Teacher.UserId == _currentUser.UserId)
            .OrderByDescending(r => r.PeriodStart)
            .Select(r => new PayrollRunDto(r.Id, r.TeacherId, r.PeriodStart, r.PeriodEnd, r.TotalAmount, r.Status))
            .ToListAsync(cancellationToken);
    }
}

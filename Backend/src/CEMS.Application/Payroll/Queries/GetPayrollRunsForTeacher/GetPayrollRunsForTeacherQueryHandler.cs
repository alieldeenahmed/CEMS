using CEMS.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payroll.Queries.GetPayrollRunsForTeacher;

public class GetPayrollRunsForTeacherQueryHandler : IRequestHandler<GetPayrollRunsForTeacherQuery, List<PayrollRunDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPayrollRunsForTeacherQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PayrollRunDto>> Handle(GetPayrollRunsForTeacherQuery request, CancellationToken cancellationToken)
    {
        return await _context.PayrollRuns
            .Where(r => r.TeacherId == request.TeacherId)
            .OrderByDescending(r => r.PeriodStart)
            .Select(r => new PayrollRunDto(r.Id, r.TeacherId, r.PeriodStart, r.PeriodEnd, r.TotalAmount, r.Status))
            .ToListAsync(cancellationToken);
    }
}

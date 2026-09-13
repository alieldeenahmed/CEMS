using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Payroll;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payroll.Queries.GetLineItemsForPayrollRun;

public class GetLineItemsForPayrollRunQueryHandler : IRequestHandler<GetLineItemsForPayrollRunQuery, List<PayrollLineItemDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetLineItemsForPayrollRunQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<PayrollLineItemDto>> Handle(GetLineItemsForPayrollRunQuery request, CancellationToken cancellationToken)
    {
        var run = await _context.PayrollRuns
            .Include(r => r.Teacher)
            .FirstOrDefaultAsync(r => r.Id == request.PayrollRunId, cancellationToken)
            ?? throw new NotFoundException(nameof(PayrollRun), request.PayrollRunId);

        var hasAccess = _currentUser.IsInRole(RoleNames.Owner) || run.Teacher.UserId == _currentUser.UserId;

        if (!hasAccess)
        {
            throw new ForbiddenAccessException("You do not have access to this payroll run.");
        }

        return await _context.PayrollLineItems
            .Where(li => li.PayrollRunId == request.PayrollRunId)
            .Select(li => new PayrollLineItemDto(li.Id, li.PayrollRunId, li.CourseSessionId, li.Amount))
            .ToListAsync(cancellationToken);
    }
}

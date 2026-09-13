using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Payroll;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payroll.Queries.GetPayrollRunById;

public class GetPayrollRunByIdQueryHandler : IRequestHandler<GetPayrollRunByIdQuery, PayrollRunDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetPayrollRunByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PayrollRunDto> Handle(GetPayrollRunByIdQuery request, CancellationToken cancellationToken)
    {
        var run = await _context.PayrollRuns
            .Include(r => r.Teacher)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PayrollRun), request.Id);

        var hasAccess = _currentUser.IsInRole(RoleNames.Owner) || run.Teacher.UserId == _currentUser.UserId;

        if (!hasAccess)
        {
            throw new ForbiddenAccessException("You do not have access to this payroll run.");
        }

        return new PayrollRunDto(run.Id, run.TeacherId, run.PeriodStart, run.PeriodEnd, run.TotalAmount, run.Status);
    }
}

using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Payroll;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payroll.Queries.GenerateStaffPayStub;

public class GenerateStaffPayStubQueryHandler : IRequestHandler<GenerateStaffPayStubQuery, byte[]>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IIdentityService _identityService;
    private readonly IPayStubGenerator _payStubGenerator;

    public GenerateStaffPayStubQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IIdentityService identityService,
        IPayStubGenerator payStubGenerator)
    {
        _context = context;
        _currentUser = currentUser;
        _identityService = identityService;
        _payStubGenerator = payStubGenerator;
    }

    public async Task<byte[]> Handle(GenerateStaffPayStubQuery request, CancellationToken cancellationToken)
    {
        var run = await _context.StaffPayrollRuns.FirstOrDefaultAsync(r => r.Id == request.StaffPayrollRunId, cancellationToken)
            ?? throw new NotFoundException(nameof(StaffPayrollRun), request.StaffPayrollRunId);

        var hasAccess = _currentUser.IsInRole(RoleNames.Owner) || run.UserId == _currentUser.UserId;

        if (!hasAccess)
        {
            throw new ForbiddenAccessException("You do not have access to this payroll run.");
        }

        var user = await _identityService.GetAuthenticatedUserAsync(run.UserId);
        var data = new PayStubData(user.FullName, run.PeriodStart, run.PeriodEnd, run.Amount, run.Status, Array.Empty<PayStubLineItem>());

        return _payStubGenerator.Generate(data);
    }
}

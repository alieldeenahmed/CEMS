using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Payroll;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payroll.Queries.GeneratePayStub;

public class GeneratePayStubQueryHandler : IRequestHandler<GeneratePayStubQuery, byte[]>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IIdentityService _identityService;
    private readonly IPayStubGenerator _payStubGenerator;

    public GeneratePayStubQueryHandler(
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

    public async Task<byte[]> Handle(GeneratePayStubQuery request, CancellationToken cancellationToken)
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

        var teacher = await _identityService.GetAuthenticatedUserAsync(run.Teacher.UserId);

        var lineItems = await _context.PayrollLineItems
            .Where(li => li.PayrollRunId == request.PayrollRunId)
            .OrderBy(li => li.CourseSession.StartUtc)
            .Select(li => new PayStubLineItem(
                li.CourseSession.Course.Name,
                li.CourseSession.StartUtc,
                li.CourseSession.EndUtc,
                li.Amount))
            .ToListAsync(cancellationToken);

        var data = new PayStubData(teacher.FullName, run.PeriodStart, run.PeriodEnd, run.TotalAmount, run.Status, lineItems);

        return _payStubGenerator.Generate(data);
    }
}

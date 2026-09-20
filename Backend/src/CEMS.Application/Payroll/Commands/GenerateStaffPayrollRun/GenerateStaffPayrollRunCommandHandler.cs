using CEMS.Application.Common.Concurrency;
using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Payroll;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payroll.Commands.GenerateStaffPayrollRun;

public class GenerateStaffPayrollRunCommandHandler : IRequestHandler<GenerateStaffPayrollRunCommand, StaffPayrollRunDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public GenerateStaffPayrollRunCommandHandler(IApplicationDbContext context, IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task<StaffPayrollRunDto> Handle(GenerateStaffPayrollRunCommand request, CancellationToken cancellationToken)
    {
        AuthenticatedUser user;
        try
        {
            user = await _identityService.GetAuthenticatedUserAsync(request.UserId);
        }
        catch (InvalidOperationException)
        {
            throw new NotFoundException("User", request.UserId);
        }

        // Teachers already have a real, session-based payroll system (see GeneratePayrollRunCommand);
        // this manual-amount path exists specifically for staff whose work isn't tracked in sessions.
        var isEligibleRole = user.Roles.Contains(RoleNames.FrontDesk) || user.Roles.Contains(RoleNames.BranchManager);
        if (!isEligibleRole)
        {
            throw new BadRequestException(new[] { "Staff payroll runs are only for Front Desk and Branch Manager accounts." });
        }

        await using var transaction = await _context.BeginLockedTransactionAsync(cancellationToken, LockKeys.StaffPayroll(request.UserId));

        var overlapping = await _context.StaffPayrollRuns.AnyAsync(
            r => r.UserId == request.UserId && r.PeriodStart <= request.PeriodEnd && request.PeriodStart <= r.PeriodEnd,
            cancellationToken);

        if (overlapping)
        {
            throw new BadRequestException(new[] { "A staff payroll run already exists for this user covering an overlapping period." });
        }

        var run = new StaffPayrollRun
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            Amount = request.Amount,
            Status = PayrollRunStatus.Draft
        };

        _context.StaffPayrollRuns.Add(run);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new StaffPayrollRunDto(run.Id, run.UserId, run.PeriodStart, run.PeriodEnd, run.Amount, run.Status);
    }
}

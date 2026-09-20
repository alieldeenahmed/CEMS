using CEMS.Application.Common.Concurrency;
using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payroll.Commands.MarkStaffPayrollRunPaid;

public class MarkStaffPayrollRunPaidCommandHandler : IRequestHandler<MarkStaffPayrollRunPaidCommand, StaffPayrollRunDto>
{
    private readonly IApplicationDbContext _context;

    public MarkStaffPayrollRunPaidCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StaffPayrollRunDto> Handle(MarkStaffPayrollRunPaidCommand request, CancellationToken cancellationToken)
    {
        // A run's status gates what can be done to it (only a Draft may be approved or have its amount edited),
        // so the check and the change must not interleave with another request on the same run -- e.g. an
        // amount edit landing after an approval that has just happened.
        await using var transaction = await _context.BeginLockedTransactionAsync(cancellationToken, LockKeys.PayrollRun(request.Id));

        var run = await _context.StaffPayrollRuns.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(StaffPayrollRun), request.Id);

        if (run.Status != PayrollRunStatus.Approved)
        {
            throw new BadRequestException(new[] { "Only an Approved payroll run can be marked as Paid." });
        }

        run.Status = PayrollRunStatus.Paid;
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new StaffPayrollRunDto(run.Id, run.UserId, run.PeriodStart, run.PeriodEnd, run.Amount, run.Status);
    }
}

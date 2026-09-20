using CEMS.Application.Common.Concurrency;
using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payroll.Commands.MarkPayrollRunPaid;

public class MarkPayrollRunPaidCommandHandler : IRequestHandler<MarkPayrollRunPaidCommand, PayrollRunDto>
{
    private readonly IApplicationDbContext _context;

    public MarkPayrollRunPaidCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PayrollRunDto> Handle(MarkPayrollRunPaidCommand request, CancellationToken cancellationToken)
    {
        // A run's status gates what can be done to it (only a Draft may be approved or have its amount edited),
        // so the check and the change must not interleave with another request on the same run -- e.g. an
        // amount edit landing after an approval that has just happened.
        await using var transaction = await _context.BeginLockedTransactionAsync(cancellationToken, LockKeys.PayrollRun(request.Id));

        var run = await _context.PayrollRuns.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PayrollRun), request.Id);

        if (run.Status != PayrollRunStatus.Approved)
        {
            throw new BadRequestException(new[] { "Only an Approved payroll run can be marked as Paid." });
        }

        run.Status = PayrollRunStatus.Paid;
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new PayrollRunDto(run.Id, run.TeacherId, run.PeriodStart, run.PeriodEnd, run.TotalAmount, run.Status);
    }
}

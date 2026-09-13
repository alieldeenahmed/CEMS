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
        var run = await _context.PayrollRuns.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PayrollRun), request.Id);

        if (run.Status != PayrollRunStatus.Approved)
        {
            throw new BadRequestException(new[] { "Only an Approved payroll run can be marked as Paid." });
        }

        run.Status = PayrollRunStatus.Paid;
        await _context.SaveChangesAsync(cancellationToken);

        return new PayrollRunDto(run.Id, run.TeacherId, run.PeriodStart, run.PeriodEnd, run.TotalAmount, run.Status);
    }
}

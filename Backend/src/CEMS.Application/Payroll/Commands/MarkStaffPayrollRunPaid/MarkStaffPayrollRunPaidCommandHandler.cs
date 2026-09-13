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
        var run = await _context.StaffPayrollRuns.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(StaffPayrollRun), request.Id);

        if (run.Status != PayrollRunStatus.Approved)
        {
            throw new BadRequestException(new[] { "Only an Approved payroll run can be marked as Paid." });
        }

        run.Status = PayrollRunStatus.Paid;
        await _context.SaveChangesAsync(cancellationToken);

        return new StaffPayrollRunDto(run.Id, run.UserId, run.PeriodStart, run.PeriodEnd, run.Amount, run.Status);
    }
}

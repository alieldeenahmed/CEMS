using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payroll.Commands.ApprovePayrollRun;

public class ApprovePayrollRunCommandHandler : IRequestHandler<ApprovePayrollRunCommand, PayrollRunDto>
{
    private readonly IApplicationDbContext _context;

    public ApprovePayrollRunCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PayrollRunDto> Handle(ApprovePayrollRunCommand request, CancellationToken cancellationToken)
    {
        var run = await _context.PayrollRuns.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PayrollRun), request.Id);

        if (run.Status != PayrollRunStatus.Draft)
        {
            throw new BadRequestException(new[] { "Only a Draft payroll run can be approved." });
        }

        run.Status = PayrollRunStatus.Approved;
        await _context.SaveChangesAsync(cancellationToken);

        return new PayrollRunDto(run.Id, run.TeacherId, run.PeriodStart, run.PeriodEnd, run.TotalAmount, run.Status);
    }
}

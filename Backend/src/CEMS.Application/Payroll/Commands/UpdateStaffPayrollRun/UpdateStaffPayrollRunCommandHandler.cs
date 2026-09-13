using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payroll.Commands.UpdateStaffPayrollRun;

public class UpdateStaffPayrollRunCommandHandler : IRequestHandler<UpdateStaffPayrollRunCommand, StaffPayrollRunDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateStaffPayrollRunCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StaffPayrollRunDto> Handle(UpdateStaffPayrollRunCommand request, CancellationToken cancellationToken)
    {
        var run = await _context.StaffPayrollRuns.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(StaffPayrollRun), request.Id);

        if (run.Status != PayrollRunStatus.Draft)
        {
            throw new BadRequestException(new[] { "Only a Draft payroll run's amount can be edited." });
        }

        run.Amount = request.Amount;
        await _context.SaveChangesAsync(cancellationToken);

        return new StaffPayrollRunDto(run.Id, run.UserId, run.PeriodStart, run.PeriodEnd, run.Amount, run.Status);
    }
}

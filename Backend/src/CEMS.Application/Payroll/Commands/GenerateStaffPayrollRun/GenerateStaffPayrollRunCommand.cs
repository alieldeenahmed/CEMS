using MediatR;

namespace CEMS.Application.Payroll.Commands.GenerateStaffPayrollRun;

public record GenerateStaffPayrollRunCommand(Guid UserId, DateOnly PeriodStart, DateOnly PeriodEnd, decimal Amount) : IRequest<StaffPayrollRunDto>;

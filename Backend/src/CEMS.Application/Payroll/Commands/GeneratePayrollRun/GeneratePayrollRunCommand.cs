using MediatR;

namespace CEMS.Application.Payroll.Commands.GeneratePayrollRun;

public record GeneratePayrollRunCommand(Guid TeacherId, DateOnly PeriodStart, DateOnly PeriodEnd) : IRequest<PayrollRunDto>;

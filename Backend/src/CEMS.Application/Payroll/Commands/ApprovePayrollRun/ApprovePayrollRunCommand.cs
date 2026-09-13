using MediatR;

namespace CEMS.Application.Payroll.Commands.ApprovePayrollRun;

public record ApprovePayrollRunCommand(Guid Id) : IRequest<PayrollRunDto>;

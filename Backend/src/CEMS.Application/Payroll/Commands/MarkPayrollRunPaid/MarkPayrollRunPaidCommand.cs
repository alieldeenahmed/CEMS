using MediatR;

namespace CEMS.Application.Payroll.Commands.MarkPayrollRunPaid;

public record MarkPayrollRunPaidCommand(Guid Id) : IRequest<PayrollRunDto>;

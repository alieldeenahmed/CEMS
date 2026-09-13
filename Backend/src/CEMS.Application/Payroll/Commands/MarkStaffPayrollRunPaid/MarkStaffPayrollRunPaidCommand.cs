using MediatR;

namespace CEMS.Application.Payroll.Commands.MarkStaffPayrollRunPaid;

public record MarkStaffPayrollRunPaidCommand(Guid Id) : IRequest<StaffPayrollRunDto>;

using MediatR;

namespace CEMS.Application.Payroll.Commands.UpdateStaffPayrollRun;

public record UpdateStaffPayrollRunCommand(Guid Id, decimal Amount) : IRequest<StaffPayrollRunDto>;

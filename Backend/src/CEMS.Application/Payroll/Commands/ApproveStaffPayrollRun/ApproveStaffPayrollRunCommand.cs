using MediatR;

namespace CEMS.Application.Payroll.Commands.ApproveStaffPayrollRun;

public record ApproveStaffPayrollRunCommand(Guid Id) : IRequest<StaffPayrollRunDto>;

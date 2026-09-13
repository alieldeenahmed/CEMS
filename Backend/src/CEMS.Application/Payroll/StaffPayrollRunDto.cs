using CEMS.Domain.Payroll;

namespace CEMS.Application.Payroll;

public record StaffPayrollRunDto(Guid Id, Guid UserId, DateOnly PeriodStart, DateOnly PeriodEnd, decimal Amount, PayrollRunStatus Status);

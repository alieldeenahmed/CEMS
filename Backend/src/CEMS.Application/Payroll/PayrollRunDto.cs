using CEMS.Domain.Payroll;

namespace CEMS.Application.Payroll;

public record PayrollRunDto(Guid Id, Guid TeacherId, DateOnly PeriodStart, DateOnly PeriodEnd, decimal TotalAmount, PayrollRunStatus Status);

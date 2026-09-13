using CEMS.Domain.Payroll;

namespace CEMS.Application.Payroll;

public record PayStubData(
    string TeacherName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal TotalAmount,
    PayrollRunStatus Status,
    IReadOnlyList<PayStubLineItem> LineItems);

public record PayStubLineItem(string CourseName, DateTime SessionStartUtc, DateTime SessionEndUtc, decimal Amount);

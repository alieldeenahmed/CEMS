namespace CEMS.Application.Payroll;

public record PayrollLineItemDto(Guid Id, Guid PayrollRunId, Guid CourseSessionId, decimal Amount);

using CEMS.Domain.Courses;

namespace CEMS.Domain.Payroll;

public class PayrollLineItem
{
    public Guid Id { get; set; }

    public Guid PayrollRunId { get; set; }
    public PayrollRun PayrollRun { get; set; } = null!;

    public Guid CourseSessionId { get; set; }
    public CourseSession CourseSession { get; set; } = null!;

    public decimal Amount { get; set; }
}

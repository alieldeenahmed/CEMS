namespace CEMS.Domain.Payroll;

public class StaffPayrollRun
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public decimal Amount { get; set; }
    public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;
}

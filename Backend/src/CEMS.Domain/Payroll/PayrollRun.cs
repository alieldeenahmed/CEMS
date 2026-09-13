using CEMS.Domain.Teachers;

namespace CEMS.Domain.Payroll;

public class PayrollRun
{
    public Guid Id { get; set; }

    public Guid TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public decimal TotalAmount { get; set; }
    public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;

    public ICollection<PayrollLineItem> LineItems { get; set; } = new List<PayrollLineItem>();
}

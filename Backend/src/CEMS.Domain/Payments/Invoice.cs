using CEMS.Domain.Students;

namespace CEMS.Domain.Payments;

public class Invoice
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public Guid? PackageId { get; set; }
    public Package? Package { get; set; }

    public decimal Amount { get; set; }
    public DateOnly IssuedDate { get; set; }
    public DateOnly DueDate { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

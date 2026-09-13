namespace CEMS.Domain.Payments;

public class Payment
{
    public Guid Id { get; set; }

    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public decimal AmountPaid { get; set; }
    public DateOnly PaymentDate { get; set; }
    public PaymentMethod Method { get; set; }
    public Guid ReceivedByUserId { get; set; }
}

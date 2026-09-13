using MediatR;

namespace CEMS.Application.Payments.Queries.GetInvoicesForStudent;

public record GetInvoicesForStudentQuery(Guid StudentId) : IRequest<List<InvoiceDto>>;

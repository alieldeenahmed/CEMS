using MediatR;

namespace CEMS.Application.Payroll.Queries.GetLineItemsForPayrollRun;

public record GetLineItemsForPayrollRunQuery(Guid PayrollRunId) : IRequest<List<PayrollLineItemDto>>;

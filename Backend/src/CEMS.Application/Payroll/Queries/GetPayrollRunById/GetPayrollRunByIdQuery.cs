using MediatR;

namespace CEMS.Application.Payroll.Queries.GetPayrollRunById;

public record GetPayrollRunByIdQuery(Guid Id) : IRequest<PayrollRunDto>;

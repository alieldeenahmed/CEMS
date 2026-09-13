using MediatR;

namespace CEMS.Application.Payroll.Queries.GetMyPayrollRuns;

public record GetMyPayrollRunsQuery : IRequest<List<PayrollRunDto>>;

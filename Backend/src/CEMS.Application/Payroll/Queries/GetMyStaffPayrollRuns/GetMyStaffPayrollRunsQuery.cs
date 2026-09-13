using MediatR;

namespace CEMS.Application.Payroll.Queries.GetMyStaffPayrollRuns;

public record GetMyStaffPayrollRunsQuery : IRequest<List<StaffPayrollRunDto>>;

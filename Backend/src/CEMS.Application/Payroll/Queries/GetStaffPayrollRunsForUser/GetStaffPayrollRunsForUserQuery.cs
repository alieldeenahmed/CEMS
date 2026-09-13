using MediatR;

namespace CEMS.Application.Payroll.Queries.GetStaffPayrollRunsForUser;

public record GetStaffPayrollRunsForUserQuery(Guid UserId) : IRequest<List<StaffPayrollRunDto>>;

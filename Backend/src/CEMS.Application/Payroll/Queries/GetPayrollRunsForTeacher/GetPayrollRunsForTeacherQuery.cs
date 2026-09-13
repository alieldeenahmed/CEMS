using MediatR;

namespace CEMS.Application.Payroll.Queries.GetPayrollRunsForTeacher;

public record GetPayrollRunsForTeacherQuery(Guid TeacherId) : IRequest<List<PayrollRunDto>>;

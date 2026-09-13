using MediatR;

namespace CEMS.Application.Payroll.Queries.GenerateStaffPayStub;

public record GenerateStaffPayStubQuery(Guid StaffPayrollRunId) : IRequest<byte[]>;

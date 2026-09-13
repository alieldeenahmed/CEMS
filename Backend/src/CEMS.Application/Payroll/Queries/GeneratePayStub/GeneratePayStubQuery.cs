using MediatR;

namespace CEMS.Application.Payroll.Queries.GeneratePayStub;

public record GeneratePayStubQuery(Guid PayrollRunId) : IRequest<byte[]>;

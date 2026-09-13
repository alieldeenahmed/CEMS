using MediatR;

namespace CEMS.Application.Payments.Queries.GetOutstandingBalanceForStudent;

public record GetOutstandingBalanceForStudentQuery(Guid StudentId) : IRequest<OutstandingBalanceDto>;

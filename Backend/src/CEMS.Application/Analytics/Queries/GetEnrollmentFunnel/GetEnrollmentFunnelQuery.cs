using MediatR;

namespace CEMS.Application.Analytics.Queries.GetEnrollmentFunnel;

public record GetEnrollmentFunnelQuery(Guid? BranchId) : IRequest<EnrollmentFunnelDto>;

using MediatR;

namespace CEMS.Application.Analytics.Queries.GetRevenueSummary;

public record GetRevenueSummaryQuery(Guid? BranchId, DateOnly PeriodStart, DateOnly PeriodEnd) : IRequest<RevenueSummaryDto>;

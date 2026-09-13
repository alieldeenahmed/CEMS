using MediatR;

namespace CEMS.Application.Analytics.Queries.GetDashboardSummary;

public record GetDashboardSummaryQuery(Guid? BranchId, DateOnly PeriodStart, DateOnly PeriodEnd) : IRequest<DashboardSummaryDto>;

using CEMS.Application.Analytics.Queries.GetAttendanceTrends;
using CEMS.Application.Analytics.Queries.GetEnrollmentFunnel;
using CEMS.Application.Analytics.Queries.GetRevenueSummary;
using CEMS.Application.Analytics.Queries.GetTeacherUtilization;
using MediatR;

namespace CEMS.Application.Analytics.Queries.GetDashboardSummary;

public class GetDashboardSummaryQueryHandler : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IMediator _mediator;

    public GetDashboardSummaryQueryHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var revenue = await _mediator.Send(new GetRevenueSummaryQuery(request.BranchId, request.PeriodStart, request.PeriodEnd), cancellationToken);
        var utilization = await _mediator.Send(new GetTeacherUtilizationQuery(request.BranchId, request.PeriodStart, request.PeriodEnd), cancellationToken);
        var attendance = await _mediator.Send(new GetAttendanceTrendsQuery(request.BranchId, request.PeriodStart, request.PeriodEnd), cancellationToken);
        var funnel = await _mediator.Send(new GetEnrollmentFunnelQuery(request.BranchId), cancellationToken);

        return new DashboardSummaryDto(request.BranchId, request.PeriodStart, request.PeriodEnd, revenue, utilization, attendance, funnel);
    }
}

using CEMS.Application.Analytics.Queries.GetDashboardSummary;
using MediatR;

namespace CEMS.Application.Analytics.Queries.ExportDashboard;

public class ExportDashboardExcelQueryHandler : IRequestHandler<ExportDashboardExcelQuery, byte[]>
{
    private readonly IMediator _mediator;
    private readonly IDashboardReportGenerator _reportGenerator;

    public ExportDashboardExcelQueryHandler(IMediator mediator, IDashboardReportGenerator reportGenerator)
    {
        _mediator = mediator;
        _reportGenerator = reportGenerator;
    }

    public async Task<byte[]> Handle(ExportDashboardExcelQuery request, CancellationToken cancellationToken)
    {
        var summary = await _mediator.Send(new GetDashboardSummaryQuery(request.BranchId, request.PeriodStart, request.PeriodEnd), cancellationToken);
        return _reportGenerator.GenerateExcel(summary);
    }
}

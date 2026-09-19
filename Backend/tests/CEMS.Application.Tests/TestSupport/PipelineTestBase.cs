using CEMS.Application.Analytics;
using CEMS.Application.Common.Interfaces;
using CEMS.Application.Exams;
using CEMS.Application.Payroll;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CEMS.Application.Tests.TestSupport;

/// <summary>Records what it was asked to render and returns fixed bytes, standing in for QuestPDF/ClosedXML.</summary>
public class CapturingReports : IDashboardReportGenerator, IReportCardGenerator, IPayStubGenerator
{
    public DashboardSummaryDto? Dashboard { get; private set; }
    public string? DashboardFormat { get; private set; }

    public byte[] GeneratePdf(DashboardSummaryDto summary)
    {
        Dashboard = summary;
        DashboardFormat = "pdf";
        return [0x25, 0x50];
    }

    public byte[] GenerateExcel(DashboardSummaryDto summary)
    {
        Dashboard = summary;
        DashboardFormat = "excel";
        return [0x50, 0x4B];
    }

    public byte[] Generate(ReportCardData data) => [1];

    public byte[] Generate(PayStubData data) => [2];
}

/// <summary>
/// Runs requests through the real MediatR pipeline (validation and audit behaviors included) against
/// the same test database and fakes the handler-level tests use.
/// </summary>
public abstract class PipelineTestBase : SeededHandlerTestBase
{
    protected readonly TestIdentityService Identity = new();
    protected readonly CapturingReports Reports = new();

    private IMediator? _mediator;

    protected IMediator Mediator => _mediator ??= BuildMediator();

    private IMediator BuildMediator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices();
        services.AddSingleton<IApplicationDbContext>(Context);
        services.AddSingleton<ICurrentUserService>(CurrentUser);
        services.AddSingleton<IIdentityService>(Identity);
        services.AddSingleton<IDashboardReportGenerator>(Reports);
        services.AddSingleton<IReportCardGenerator>(Reports);
        services.AddSingleton<IPayStubGenerator>(Reports);

        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }
}

namespace CEMS.Application.Analytics;

public interface IDashboardReportGenerator
{
    byte[] GeneratePdf(DashboardSummaryDto summary);
    byte[] GenerateExcel(DashboardSummaryDto summary);
}

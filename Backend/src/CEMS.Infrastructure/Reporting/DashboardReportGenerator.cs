using ClosedXML.Excel;
using CEMS.Application.Analytics;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CEMS.Infrastructure.Reporting;

public class DashboardReportGenerator : IDashboardReportGenerator
{
    public byte[] GeneratePdf(DashboardSummaryDto summary)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(header =>
                {
                    header.Item().Text("Analytics Dashboard").FontSize(20).Bold();
                    header.Item().Text($"{summary.PeriodStart:yyyy-MM-dd} to {summary.PeriodEnd:yyyy-MM-dd}"
                        + (summary.BranchId.HasValue ? $" — Branch {summary.BranchId}" : " — All Branches"));
                });

                page.Content().PaddingTop(20).Column(column =>
                {
                    column.Spacing(20);

                    column.Item().Column(section =>
                    {
                        section.Item().Text("Revenue").FontSize(14).Bold();
                        section.Item().Text($"Invoiced: {summary.Revenue.TotalInvoiced:0.00}");
                        section.Item().Text($"Collected: {summary.Revenue.TotalCollected:0.00}");
                        section.Item().Text($"Outstanding: {summary.Revenue.TotalOutstanding:0.00}");
                    });

                    column.Item().Column(section =>
                    {
                        section.Item().Text("Teacher Utilization").FontSize(14).Bold();

                        if (summary.TeacherUtilization.Count == 0)
                        {
                            section.Item().Text("No teachers in scope.").Italic();
                        }
                        else
                        {
                            section.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(h =>
                                {
                                    h.Cell().Text("Teacher").Bold();
                                    h.Cell().Text("Scheduled Hrs").Bold();
                                    h.Cell().Text("Available Hrs").Bold();
                                    h.Cell().Text("Utilization").Bold();
                                });

                                foreach (var t in summary.TeacherUtilization)
                                {
                                    table.Cell().Text(t.TeacherId.ToString());
                                    table.Cell().Text(t.ScheduledHours.ToString("0.0"));
                                    table.Cell().Text(t.AvailableHours.ToString("0.0"));
                                    table.Cell().Text($"{t.UtilizationRate:P0}");
                                }
                            });
                        }
                    });

                    column.Item().Column(section =>
                    {
                        section.Item().Text("Attendance Trends").FontSize(14).Bold();
                        section.Item().Text($"Total records: {summary.AttendanceTrends.TotalRecords}");
                        section.Item().Text($"Present: {summary.AttendanceTrends.PresentCount}");
                        section.Item().Text($"Absent: {summary.AttendanceTrends.AbsentCount}");
                        section.Item().Text($"Late: {summary.AttendanceTrends.LateCount}");
                        section.Item().Text($"Excused: {summary.AttendanceTrends.ExcusedCount}");
                        section.Item().Text($"Attendance rate: {summary.AttendanceTrends.AttendanceRate:P0}");
                    });

                    column.Item().Column(section =>
                    {
                        section.Item().Text("Enrollment Funnel").FontSize(14).Bold();
                        section.Item().Text($"Total students: {summary.EnrollmentFunnel.TotalStudents}");
                        section.Item().Text($"With any enrollment: {summary.EnrollmentFunnel.StudentsWithAnyEnrollment}");
                        section.Item().Text($"With active enrollment: {summary.EnrollmentFunnel.StudentsWithActiveEnrollment}");
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Generated ");
                    text.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'"));
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateExcel(DashboardSummaryDto summary)
    {
        using var workbook = new XLWorkbook();

        var revenueSheet = workbook.Worksheets.Add("Revenue");
        revenueSheet.Cell(1, 1).Value = "Metric";
        revenueSheet.Cell(1, 2).Value = "Amount";
        revenueSheet.Cell(2, 1).Value = "Total Invoiced";
        revenueSheet.Cell(2, 2).Value = summary.Revenue.TotalInvoiced;
        revenueSheet.Cell(3, 1).Value = "Total Collected";
        revenueSheet.Cell(3, 2).Value = summary.Revenue.TotalCollected;
        revenueSheet.Cell(4, 1).Value = "Total Outstanding";
        revenueSheet.Cell(4, 2).Value = summary.Revenue.TotalOutstanding;

        var utilizationSheet = workbook.Worksheets.Add("Teacher Utilization");
        utilizationSheet.Cell(1, 1).Value = "TeacherId";
        utilizationSheet.Cell(1, 2).Value = "Scheduled Hours";
        utilizationSheet.Cell(1, 3).Value = "Available Hours";
        utilizationSheet.Cell(1, 4).Value = "Utilization Rate";
        for (var i = 0; i < summary.TeacherUtilization.Count; i++)
        {
            var t = summary.TeacherUtilization[i];
            var row = i + 2;
            utilizationSheet.Cell(row, 1).Value = t.TeacherId.ToString();
            utilizationSheet.Cell(row, 2).Value = t.ScheduledHours;
            utilizationSheet.Cell(row, 3).Value = t.AvailableHours;
            utilizationSheet.Cell(row, 4).Value = t.UtilizationRate;
        }

        var attendanceSheet = workbook.Worksheets.Add("Attendance Trends");
        attendanceSheet.Cell(1, 1).Value = "Metric";
        attendanceSheet.Cell(1, 2).Value = "Value";
        attendanceSheet.Cell(2, 1).Value = "Total Records";
        attendanceSheet.Cell(2, 2).Value = summary.AttendanceTrends.TotalRecords;
        attendanceSheet.Cell(3, 1).Value = "Present";
        attendanceSheet.Cell(3, 2).Value = summary.AttendanceTrends.PresentCount;
        attendanceSheet.Cell(4, 1).Value = "Absent";
        attendanceSheet.Cell(4, 2).Value = summary.AttendanceTrends.AbsentCount;
        attendanceSheet.Cell(5, 1).Value = "Late";
        attendanceSheet.Cell(5, 2).Value = summary.AttendanceTrends.LateCount;
        attendanceSheet.Cell(6, 1).Value = "Excused";
        attendanceSheet.Cell(6, 2).Value = summary.AttendanceTrends.ExcusedCount;
        attendanceSheet.Cell(7, 1).Value = "Attendance Rate";
        attendanceSheet.Cell(7, 2).Value = summary.AttendanceTrends.AttendanceRate;

        var funnelSheet = workbook.Worksheets.Add("Enrollment Funnel");
        funnelSheet.Cell(1, 1).Value = "Stage";
        funnelSheet.Cell(1, 2).Value = "Count";
        funnelSheet.Cell(2, 1).Value = "Total Students";
        funnelSheet.Cell(2, 2).Value = summary.EnrollmentFunnel.TotalStudents;
        funnelSheet.Cell(3, 1).Value = "With Any Enrollment";
        funnelSheet.Cell(3, 2).Value = summary.EnrollmentFunnel.StudentsWithAnyEnrollment;
        funnelSheet.Cell(4, 1).Value = "With Active Enrollment";
        funnelSheet.Cell(4, 2).Value = summary.EnrollmentFunnel.StudentsWithActiveEnrollment;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}

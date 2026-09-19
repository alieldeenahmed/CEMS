using System.Text;
using CEMS.Application.Analytics;
using CEMS.Application.Exams;
using CEMS.Application.Payroll;
using CEMS.Domain.Payroll;
using CEMS.Infrastructure.Reporting;
using ClosedXML.Excel;
using QuestPDF.Infrastructure;

namespace CEMS.Application.Tests.Infrastructure;

/// <summary>
/// Renders the real QuestPDF and ClosedXML output. PDFs are checked for a valid header and sensible size;
/// the Excel export is reopened and its cells read back, so the numbers are verified, not just the file type.
/// </summary>
public class ReportGeneratorTests
{
    public ReportGeneratorTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static DashboardSummaryDto Summary() => new(
        null, new DateOnly(2030, 1, 1), new DateOnly(2030, 1, 31),
        new RevenueSummaryDto(1800m, 700m, 1000m),
        [new TeacherUtilizationDto(Guid.NewGuid(), "Ahmed Nabil", 3.0, 24.0, 0.125)],
        new AttendanceTrendsDto(6, 3, 1, 1, 1, 0.5),
        new EnrollmentFunnelDto(5, 3, 1));

    private static void AssertLooksLikeAPdf(byte[] bytes)
    {
        Assert.True(bytes.Length > 1000, $"PDF is suspiciously small ({bytes.Length} bytes)");
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Fact]
    public void DashboardPdf_IsAValidPdf()
    {
        AssertLooksLikeAPdf(new DashboardReportGenerator().GeneratePdf(Summary()));
    }

    [Fact]
    public void DashboardPdf_WithNoTeachers_StillRenders()
    {
        var empty = Summary() with { TeacherUtilization = [] };

        AssertLooksLikeAPdf(new DashboardReportGenerator().GeneratePdf(empty));
    }

    [Fact]
    public void DashboardExcel_ContainsEveryMetricOnItsOwnSheet()
    {
        var bytes = new DashboardReportGenerator().GenerateExcel(Summary());

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(["Revenue", "Teacher Utilization", "Attendance Trends", "Enrollment Funnel"], workbook.Worksheets.Select(w => w.Name).ToArray());

        var revenue = workbook.Worksheet("Revenue");
        Assert.Equal(1800.0, revenue.Cell(2, 2).GetDouble());
        Assert.Equal(700.0, revenue.Cell(3, 2).GetDouble());
        Assert.Equal(1000.0, revenue.Cell(4, 2).GetDouble());

        var utilization = workbook.Worksheet("Teacher Utilization");
        Assert.Equal("Ahmed Nabil", utilization.Cell(2, 1).GetString());
        Assert.Equal(0.125, utilization.Cell(2, 4).GetDouble());

        var attendance = workbook.Worksheet("Attendance Trends");
        Assert.Equal(6, attendance.Cell(2, 2).GetDouble());
        Assert.Equal(0.5, attendance.Cell(7, 2).GetDouble());

        var funnel = workbook.Worksheet("Enrollment Funnel");
        Assert.Equal([5.0, 3.0, 1.0], new[] { funnel.Cell(2, 2).GetDouble(), funnel.Cell(3, 2).GetDouble(), funnel.Cell(4, 2).GetDouble() });
    }

    [Fact]
    public void ReportCardPdf_RendersWithGradedAndUngradedExams()
    {
        var data = new ReportCardData("Khaled Hany",
        [
            new ReportCardCourseSection("Python Fundamentals",
            [
                new ReportCardExamRow("Midterm", new DateOnly(2030, 2, 1), 100, 88),
                new ReportCardExamRow("Final", new DateOnly(2030, 5, 1), 100, null)
            ]),
            new ReportCardCourseSection("Scratch", [])
        ]);

        AssertLooksLikeAPdf(new QuestPdfReportCardGenerator().Generate(data));
    }

    [Fact]
    public void ReportCardPdf_ForAStudentWithNoCourses_StillRenders()
    {
        AssertLooksLikeAPdf(new QuestPdfReportCardGenerator().Generate(new ReportCardData("New Student", [])));
    }

    [Theory]
    [InlineData(PayrollRunStatus.Draft)]
    [InlineData(PayrollRunStatus.Approved)]
    [InlineData(PayrollRunStatus.Paid)]
    public void PayStubPdf_RendersInEveryStatus_WithAndWithoutLineItems(PayrollRunStatus status)
    {
        var withItems = new PayStubData("Ahmed Nabil", new DateOnly(2030, 1, 1), new DateOnly(2030, 1, 31), 300, status,
        [
            new PayStubLineItem("Python Fundamentals", new DateTime(2030, 1, 7, 10, 0, 0, DateTimeKind.Utc), new DateTime(2030, 1, 7, 11, 0, 0, DateTimeKind.Utc), 150),
            new PayStubLineItem("Python Fundamentals", new DateTime(2030, 1, 14, 10, 0, 0, DateTimeKind.Utc), new DateTime(2030, 1, 14, 11, 0, 0, DateTimeKind.Utc), 150)
        ]);
        var generator = new QuestPdfPayStubGenerator();

        AssertLooksLikeAPdf(generator.Generate(withItems));
        AssertLooksLikeAPdf(generator.Generate(withItems with { LineItems = [] }));
    }
}

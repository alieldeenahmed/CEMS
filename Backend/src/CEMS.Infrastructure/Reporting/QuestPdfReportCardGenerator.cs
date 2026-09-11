using CEMS.Application.Exams;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CEMS.Infrastructure.Reporting;

public class QuestPdfReportCardGenerator : IReportCardGenerator
{
    public byte[] Generate(ReportCardData data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header()
                    .Text($"Report Card — {data.StudentName}")
                    .FontSize(20).Bold();

                page.Content().PaddingTop(20).Column(column =>
                {
                    column.Spacing(20);

                    if (data.Courses.Count == 0)
                    {
                        column.Item().Text("No course enrollments on record.");
                        return;
                    }

                    foreach (var course in data.Courses)
                    {
                        column.Item().Column(courseColumn =>
                        {
                            courseColumn.Item().Text(course.CourseName).FontSize(14).Bold();

                            if (course.Exams.Count == 0)
                            {
                                courseColumn.Item().Text("No exams recorded.").Italic();
                                return;
                            }

                            courseColumn.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Text("Exam").Bold();
                                    header.Cell().Text("Date").Bold();
                                    header.Cell().Text("Score").Bold();
                                    header.Cell().Text("Max").Bold();
                                });

                                foreach (var exam in course.Exams)
                                {
                                    table.Cell().Text(exam.ExamName);
                                    table.Cell().Text(exam.ExamDate.ToString("yyyy-MM-dd"));
                                    table.Cell().Text(exam.Score?.ToString("0.##") ?? "—");
                                    table.Cell().Text(exam.MaxScore.ToString("0.##"));
                                }
                            });
                        });
                    }
                });

                page.Footer()
                    .AlignCenter()
                    .Text(text =>
                    {
                        text.Span("Generated ");
                        text.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'"));
                    });
            });
        });

        return document.GeneratePdf();
    }
}

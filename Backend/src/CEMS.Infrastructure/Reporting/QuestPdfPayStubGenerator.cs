using CEMS.Application.Payroll;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CEMS.Infrastructure.Reporting;

public class QuestPdfPayStubGenerator : IPayStubGenerator
{
    public byte[] Generate(PayStubData data)
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
                    header.Item().Text("Pay Stub").FontSize(20).Bold();
                    header.Item().Text(data.TeacherName).FontSize(14);
                    header.Item().Text($"{data.PeriodStart:yyyy-MM-dd} to {data.PeriodEnd:yyyy-MM-dd} — {data.Status}");
                });

                page.Content().PaddingTop(20).Column(column =>
                {
                    column.Spacing(20);

                    column.Item().Column(section =>
                    {
                        section.Item().Text("Sessions").FontSize(14).Bold();

                        if (data.LineItems.Count == 0)
                        {
                            section.Item().Text("No sessions in this run.").Italic();
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
                                });

                                table.Header(h =>
                                {
                                    h.Cell().Text("Course").Bold();
                                    h.Cell().Text("Session").Bold();
                                    h.Cell().Text("Amount").Bold();
                                });

                                foreach (var item in data.LineItems)
                                {
                                    table.Cell().Text(item.CourseName);
                                    table.Cell().Text(item.SessionStartUtc.ToString("yyyy-MM-dd HH:mm"));
                                    table.Cell().Text(item.Amount.ToString("0.00"));
                                }
                            });
                        }
                    });

                    column.Item().AlignRight().Text($"Total: {data.TotalAmount:0.00}").FontSize(16).Bold();
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
}

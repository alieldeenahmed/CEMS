namespace CEMS.Application.Exams;

public interface IReportCardGenerator
{
    byte[] Generate(ReportCardData data);
}

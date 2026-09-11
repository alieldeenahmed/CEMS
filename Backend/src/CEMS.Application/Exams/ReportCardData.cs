namespace CEMS.Application.Exams;

public record ReportCardData(string StudentName, IReadOnlyList<ReportCardCourseSection> Courses);

public record ReportCardCourseSection(string CourseName, IReadOnlyList<ReportCardExamRow> Exams);

public record ReportCardExamRow(string ExamName, DateOnly ExamDate, decimal MaxScore, decimal? Score);

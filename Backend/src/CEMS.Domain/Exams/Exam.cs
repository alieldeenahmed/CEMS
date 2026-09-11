using CEMS.Domain.Courses;

namespace CEMS.Domain.Exams;

public class Exam
{
    public Guid Id { get; set; }

    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public decimal MaxScore { get; set; }
    public DateOnly ExamDate { get; set; }

    public ICollection<Grade> Grades { get; set; } = new List<Grade>();
}

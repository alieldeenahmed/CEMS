namespace CEMS.Domain.Courses;

public class Subject
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public Guid CurriculumId { get; set; }
    public Curriculum Curriculum { get; set; } = null!;

    public ICollection<Course> Courses { get; set; } = new List<Course>();
}

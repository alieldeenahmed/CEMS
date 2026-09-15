namespace CEMS.Domain.Courses;

public class Curriculum
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public ICollection<Course> Courses { get; set; } = new List<Course>();
}

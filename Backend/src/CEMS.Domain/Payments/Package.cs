using CEMS.Domain.Courses;

namespace CEMS.Domain.Payments;

public class Package
{
    public Guid Id { get; set; }

    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public int SessionCount { get; set; }
    public decimal Price { get; set; }
}

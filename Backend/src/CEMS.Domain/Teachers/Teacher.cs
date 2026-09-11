namespace CEMS.Domain.Teachers;

public class Teacher
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly HireDate { get; set; }
    public PayType PayType { get; set; }
    public decimal PayRate { get; set; }

    public ICollection<TeacherBranch> TeacherBranches { get; set; } = new List<TeacherBranch>();
    public ICollection<TeacherAvailability> Availabilities { get; set; } = new List<TeacherAvailability>();
}

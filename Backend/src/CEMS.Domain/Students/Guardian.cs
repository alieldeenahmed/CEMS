namespace CEMS.Domain.Students;

public class Guardian
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public Guid? UserId { get; set; }

    public ICollection<StudentGuardian> StudentGuardians { get; set; } = new List<StudentGuardian>();
}

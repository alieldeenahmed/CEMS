namespace CEMS.Domain.Branches;

public class Room
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
}

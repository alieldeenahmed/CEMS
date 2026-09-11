namespace CEMS.Application.Branches;

public record BranchDto(Guid Id, string Name, string Address, string Phone, bool IsActive);

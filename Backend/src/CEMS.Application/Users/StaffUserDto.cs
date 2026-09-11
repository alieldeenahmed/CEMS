namespace CEMS.Application.Users;

public record StaffUserDto(Guid UserId, string Email, string FullName, IReadOnlyList<string> Roles, IReadOnlyList<Guid> BranchIds);

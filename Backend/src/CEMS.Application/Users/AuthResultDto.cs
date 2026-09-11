namespace CEMS.Application.Users;

public record AuthResultDto(Guid UserId, string Email, string FullName, IReadOnlyList<string> Roles, string Token, DateTime ExpiresAtUtc);

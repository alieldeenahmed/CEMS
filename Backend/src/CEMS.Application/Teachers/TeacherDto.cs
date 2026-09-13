using CEMS.Domain.Teachers;

namespace CEMS.Application.Teachers;

public record TeacherDto(Guid Id, Guid UserId, string FullName, string Email, DateOnly HireDate, PayType PayType, decimal PayRate, IReadOnlyList<Guid> BranchIds);

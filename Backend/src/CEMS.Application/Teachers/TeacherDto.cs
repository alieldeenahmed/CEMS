using CEMS.Domain.Teachers;

namespace CEMS.Application.Teachers;

public record TeacherDto(Guid Id, Guid UserId, DateOnly HireDate, PayType PayType, decimal PayRate, IReadOnlyList<Guid> BranchIds);

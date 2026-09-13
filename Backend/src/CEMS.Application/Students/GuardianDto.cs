using CEMS.Domain.Students;

namespace CEMS.Application.Students;

public record GuardianDto(Guid Id, string FullName, string Phone, string Email)
{
    public static GuardianDto FromEntity(Guardian guardian) =>
        new(guardian.Id, guardian.FullName, guardian.Phone, guardian.Email);
}

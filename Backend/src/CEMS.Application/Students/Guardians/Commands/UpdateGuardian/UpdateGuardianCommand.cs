using CEMS.Application.Students;
using MediatR;

namespace CEMS.Application.Students.Guardians.Commands.UpdateGuardian;

public record UpdateGuardianCommand(Guid Id, string FullName, string Phone, string Email) : IRequest<GuardianDto>;

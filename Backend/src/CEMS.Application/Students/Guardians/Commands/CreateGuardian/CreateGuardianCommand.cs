using CEMS.Application.Students;
using MediatR;

namespace CEMS.Application.Students.Guardians.Commands.CreateGuardian;

public record CreateGuardianCommand(string FullName, string Phone, string Email) : IRequest<GuardianDto>;

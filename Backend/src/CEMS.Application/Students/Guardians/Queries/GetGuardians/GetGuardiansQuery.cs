using CEMS.Application.Students;
using MediatR;

namespace CEMS.Application.Students.Guardians.Queries.GetGuardians;

public record GetGuardiansQuery : IRequest<List<GuardianDto>>;

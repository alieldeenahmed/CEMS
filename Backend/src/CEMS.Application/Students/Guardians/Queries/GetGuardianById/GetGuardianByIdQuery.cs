using CEMS.Application.Students;
using MediatR;

namespace CEMS.Application.Students.Guardians.Queries.GetGuardianById;

public record GetGuardianByIdQuery(Guid Id) : IRequest<GuardianDto>;

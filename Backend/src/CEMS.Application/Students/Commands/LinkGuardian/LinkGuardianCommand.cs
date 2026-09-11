using CEMS.Domain.Students;
using MediatR;

namespace CEMS.Application.Students.Commands.LinkGuardian;

public record LinkGuardianCommand(Guid StudentId, Guid GuardianId, RelationshipType RelationshipType, bool IsPrimaryContact) : IRequest;

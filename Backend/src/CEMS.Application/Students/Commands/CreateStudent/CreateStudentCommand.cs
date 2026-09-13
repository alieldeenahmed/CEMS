using CEMS.Domain.Students;
using MediatR;

namespace CEMS.Application.Students.Commands.CreateStudent;

// A student must always have at least one guardian on record - this is enforced here rather than
// left to a separate follow-up call, so it's never possible to end up with a guardian-less student
// no matter which client creates one.
public record CreateStudentCommand(
    string FullName,
    DateOnly DateOfBirth,
    Gender Gender,
    Guid BranchId,
    Guid? ExistingGuardianId,
    string? NewGuardianFullName,
    string? NewGuardianPhone,
    string? NewGuardianEmail,
    RelationshipType RelationshipType,
    bool IsPrimaryContact) : IRequest<StudentDto>;

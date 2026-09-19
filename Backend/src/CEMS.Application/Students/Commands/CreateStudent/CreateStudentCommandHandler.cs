using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Application.Students.Guardians;
using CEMS.Domain.Branches;
using CEMS.Domain.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Students.Commands.CreateStudent;

public class CreateStudentCommandHandler : IRequestHandler<CreateStudentCommand, StudentDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateStudentCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<StudentDto> Handle(CreateStudentCommand request, CancellationToken cancellationToken)
    {
        var branchExists = await _context.Branches.AnyAsync(b => b.Id == request.BranchId, cancellationToken);
        if (!branchExists)
        {
            throw new NotFoundException(nameof(Branch), request.BranchId);
        }

        if (!_currentUser.HasAccessToBranch(request.BranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }

        Guid guardianId;
        if (request.ExistingGuardianId.HasValue)
        {
            var guardianExists = await _context.Guardians.VisibleTo(_currentUser).AnyAsync(g => g.Id == request.ExistingGuardianId.Value, cancellationToken);
            if (!guardianExists)
            {
                throw new NotFoundException(nameof(Guardian), request.ExistingGuardianId.Value);
            }

            guardianId = request.ExistingGuardianId.Value;
        }
        else
        {
            var guardian = new Guardian
            {
                Id = Guid.NewGuid(),
                FullName = request.NewGuardianFullName!,
                Phone = request.NewGuardianPhone!,
                Email = request.NewGuardianEmail!
            };

            _context.Guardians.Add(guardian);
            guardianId = guardian.Id;
        }

        var student = new Student
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            EnrollmentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = StudentStatus.Active,
            CurrentBranchId = request.BranchId
        };

        _context.Students.Add(student);

        _context.StudentGuardians.Add(new StudentGuardian
        {
            StudentId = student.Id,
            GuardianId = guardianId,
            RelationshipType = request.RelationshipType,
            IsPrimaryContact = request.IsPrimaryContact
        });

        // A single SaveChangesAsync wraps the guardian (if new), student, and link in one
        // transaction, so a student can never end up persisted without its guardian.
        await _context.SaveChangesAsync(cancellationToken);

        return StudentDto.FromEntity(student);
    }
}

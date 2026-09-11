using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
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
        await _context.SaveChangesAsync(cancellationToken);

        return StudentDto.FromEntity(student);
    }
}

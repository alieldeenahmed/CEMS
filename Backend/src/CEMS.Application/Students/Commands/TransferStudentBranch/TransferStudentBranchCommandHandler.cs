using CEMS.Application.Common.Concurrency;
using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Branches;
using CEMS.Domain.Courses;
using CEMS.Domain.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Students.Commands.TransferStudentBranch;

public class TransferStudentBranchCommandHandler : IRequestHandler<TransferStudentBranchCommand, StudentDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public TransferStudentBranchCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<StudentDto> Handle(TransferStudentBranchCommand request, CancellationToken cancellationToken)
    {
        // Read the student's current branch only once the lock is held: two simultaneous transfers would
        // otherwise both record the same "from" branch and leave the history contradicting the student.
        await using var transaction = await _context.BeginLockedTransactionAsync(cancellationToken, LockKeys.StudentBranch(request.StudentId));

        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Student), request.StudentId);

        _currentUser.EnsureAccessToBranch(student.CurrentBranchId);

        var newBranchExists = await _context.Branches.AnyAsync(b => b.Id == request.NewBranchId, cancellationToken);
        if (!newBranchExists)
        {
            throw new NotFoundException(nameof(Branch), request.NewBranchId);
        }

        if (request.NewBranchId == student.CurrentBranchId)
        {
            throw new BadRequestException(new[] { "This student is already at that branch." });
        }

        // Enrollments belong to courses at one branch, and a student may only be enrolled in their own branch's
        // courses. Moving them while they still hold a seat (or a waitlist place) would silently break that, so
        // the old enrollments must be dropped first -- an explicit decision, not a side effect of the transfer.
        var hasLiveEnrollments = await _context.CourseEnrollments.AnyAsync(
            e => e.StudentId == student.Id && e.Status != CourseEnrollmentStatus.Dropped, cancellationToken);

        if (hasLiveEnrollments)
        {
            throw new BadRequestException(new[] { "This student still has active or waitlisted enrollments at their current branch. Drop them before transferring." });
        }

        _context.StudentBranchHistories.Add(new StudentBranchHistory
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            FromBranchId = student.CurrentBranchId,
            ToBranchId = request.NewBranchId,
            TransferDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Reason = request.Reason,
            TransferredByUserId = _currentUser.UserId
        });

        student.CurrentBranchId = request.NewBranchId;

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return StudentDto.FromEntity(student);
    }
}

using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Branches;
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
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Student), request.StudentId);

        if (!_currentUser.HasAccessToBranch(student.CurrentBranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }

        var newBranchExists = await _context.Branches.AnyAsync(b => b.Id == request.NewBranchId, cancellationToken);
        if (!newBranchExists)
        {
            throw new NotFoundException(nameof(Branch), request.NewBranchId);
        }

        if (request.NewBranchId == student.CurrentBranchId)
        {
            throw new BadRequestException(new[] { "This student is already at that branch." });
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

        return StudentDto.FromEntity(student);
    }
}

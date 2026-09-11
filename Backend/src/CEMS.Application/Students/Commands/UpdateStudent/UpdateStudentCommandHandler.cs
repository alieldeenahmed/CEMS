using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Students.Commands.UpdateStudent;

public class UpdateStudentCommandHandler : IRequestHandler<UpdateStudentCommand, StudentDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateStudentCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<StudentDto> Handle(UpdateStudentCommand request, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Student), request.Id);

        if (!_currentUser.HasAccessToBranch(student.CurrentBranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }

        student.FullName = request.FullName;
        student.DateOfBirth = request.DateOfBirth;
        student.Gender = request.Gender;
        student.Status = request.Status;

        await _context.SaveChangesAsync(cancellationToken);

        return StudentDto.FromEntity(student);
    }
}

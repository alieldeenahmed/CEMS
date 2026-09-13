using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Students;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Exams.Queries.GetGradesForStudent;

public class GetGradesForStudentQueryHandler : IRequestHandler<GetGradesForStudentQuery, List<GradeDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetGradesForStudentQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<GradeDto>> Handle(GetGradesForStudentQuery request, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Student), request.StudentId);

        var hasAccess = _currentUser.IsInRole(RoleNames.Owner)
            || (_currentUser.IsInRole(RoleNames.Parent) && await _context.StudentGuardians
                .AnyAsync(sg => sg.StudentId == student.Id && sg.Guardian.UserId == _currentUser.UserId, cancellationToken))
            || _currentUser.HasAccessToBranch(student.CurrentBranchId);

        if (!hasAccess)
        {
            throw new ForbiddenAccessException("You do not have access to this student.");
        }

        return await _context.Grades
            .Where(g => g.StudentId == request.StudentId)
            .OrderByDescending(g => g.Exam.ExamDate)
            .Select(g => new GradeDto(g.Id, g.ExamId, g.Exam.Name, g.Exam.MaxScore, g.StudentId, student.FullName, g.Score, g.Comments, g.GradedAtUtc, g.GradedByUserId))
            .ToListAsync(cancellationToken);
    }
}

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

        var isFullAccess = _currentUser.IsInRole(RoleNames.Owner) || _currentUser.HasAccessToBranch(student.CurrentBranchId);

        var teacherCourseIds = _context.CourseSessions
            .Where(s => s.Teacher.UserId == _currentUser.UserId)
            .Select(s => s.CourseId);

        var isTeachingStudent = _currentUser.IsInRole(RoleNames.Teacher)
            && await _context.CourseEnrollments.AnyAsync(e => e.StudentId == request.StudentId && teacherCourseIds.Contains(e.CourseId), cancellationToken);

        if (!isFullAccess && !isTeachingStudent)
        {
            throw new ForbiddenAccessException("You do not have access to this student.");
        }

        var query = _context.Grades.Where(g => g.StudentId == request.StudentId);

        if (!isFullAccess)
        {
            query = query.Where(g => teacherCourseIds.Contains(g.Exam.CourseId));
        }

        return await query
            .OrderByDescending(g => g.Exam.ExamDate)
            .Select(g => new GradeDto(
                g.Id,
                g.ExamId,
                g.Exam.Name,
                g.Exam.MaxScore,
                g.Exam.ExamDate,
                g.Exam.CourseId,
                g.Exam.Course.Name,
                g.StudentId,
                student.FullName,
                g.Score,
                g.Comments,
                g.GradedAtUtc,
                g.GradedByUserId))
            .ToListAsync(cancellationToken);
    }
}

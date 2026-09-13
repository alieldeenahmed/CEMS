using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using CEMS.Domain.Exams;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Exams.Queries.GetGradesForExam;

public class GetGradesForExamQueryHandler : IRequestHandler<GetGradesForExamQuery, List<GradeDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetGradesForExamQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<GradeDto>> Handle(GetGradesForExamQuery request, CancellationToken cancellationToken)
    {
        var exam = await _context.Exams
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == request.ExamId, cancellationToken)
            ?? throw new NotFoundException(nameof(Exam), request.ExamId);

        var hasAccess = _currentUser.HasAccessToBranch(exam.Course.BranchId)
            || await _context.CourseSessions.AnyAsync(s => s.CourseId == exam.CourseId && s.Teacher.UserId == _currentUser.UserId, cancellationToken);

        if (!hasAccess)
        {
            throw new ForbiddenAccessException("You do not have access to this exam.");
        }

        var enrolledStudents = await _context.CourseEnrollments
            .Where(e => e.CourseId == exam.CourseId && e.Status == CourseEnrollmentStatus.Active)
            .Select(e => new { e.StudentId, e.Student.FullName })
            .OrderBy(e => e.FullName)
            .ToListAsync(cancellationToken);

        var existingGrades = await _context.Grades
            .Where(g => g.ExamId == request.ExamId)
            .ToDictionaryAsync(g => g.StudentId, cancellationToken);

        return enrolledStudents
            .Select(student => existingGrades.TryGetValue(student.StudentId, out var grade)
                ? new GradeDto(grade.Id, grade.ExamId, exam.Name, exam.MaxScore, exam.ExamDate, exam.CourseId, exam.Course.Name, grade.StudentId, student.FullName, grade.Score, grade.Comments, grade.GradedAtUtc, grade.GradedByUserId)
                : new GradeDto(null, request.ExamId, exam.Name, exam.MaxScore, exam.ExamDate, exam.CourseId, exam.Course.Name, student.StudentId, student.FullName, null, null, null, null))
            .ToList();
    }
}

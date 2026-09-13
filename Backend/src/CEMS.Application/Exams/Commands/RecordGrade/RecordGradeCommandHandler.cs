using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using CEMS.Domain.Exams;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Exams.Commands.RecordGrade;

public class RecordGradeCommandHandler : IRequestHandler<RecordGradeCommand, GradeDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RecordGradeCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<GradeDto> Handle(RecordGradeCommand request, CancellationToken cancellationToken)
    {
        var exam = await _context.Exams
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == request.ExamId, cancellationToken)
            ?? throw new NotFoundException(nameof(Exam), request.ExamId);

        var hasAccess = _currentUser.IsInRole(RoleNames.Owner)
            || (_currentUser.IsInRole(RoleNames.BranchManager) && _currentUser.HasAccessToBranch(exam.Course.BranchId))
            || (_currentUser.IsInRole(RoleNames.Teacher) && await _context.CourseSessions
                .AnyAsync(s => s.CourseId == exam.CourseId && s.Teacher.UserId == _currentUser.UserId, cancellationToken));

        if (!hasAccess)
        {
            throw new ForbiddenAccessException("You do not have access to record grades for this exam.");
        }

        var studentFullName = await _context.CourseEnrollments
            .Where(e => e.StudentId == request.StudentId && e.CourseId == exam.CourseId && e.Status == CourseEnrollmentStatus.Active)
            .Select(e => e.Student.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        if (studentFullName is null)
        {
            throw new BadRequestException(new[] { "This student is not actively enrolled in this exam's course." });
        }

        if (request.Score > exam.MaxScore)
        {
            throw new BadRequestException(new[] { $"Score cannot exceed the exam's maximum score of {exam.MaxScore}." });
        }

        var grade = await _context.Grades.FirstOrDefaultAsync(
            g => g.ExamId == request.ExamId && g.StudentId == request.StudentId,
            cancellationToken);

        if (grade is null)
        {
            grade = new Grade
            {
                Id = Guid.NewGuid(),
                ExamId = request.ExamId,
                StudentId = request.StudentId
            };
            _context.Grades.Add(grade);
        }

        grade.Score = request.Score;
        grade.Comments = request.Comments;
        grade.GradedAtUtc = DateTime.UtcNow;
        grade.GradedByUserId = _currentUser.UserId;

        await _context.SaveChangesAsync(cancellationToken);

        return new GradeDto(
            grade.Id,
            grade.ExamId,
            exam.Name,
            exam.MaxScore,
            exam.ExamDate,
            exam.CourseId,
            exam.Course.Name,
            grade.StudentId,
            studentFullName,
            grade.Score,
            grade.Comments,
            grade.GradedAtUtc,
            grade.GradedByUserId);
    }
}

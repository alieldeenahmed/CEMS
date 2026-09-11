using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using CEMS.Domain.Exams;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Exams.Commands.CreateExam;

public class CreateExamCommandHandler : IRequestHandler<CreateExamCommand, ExamDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateExamCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ExamDto> Handle(CreateExamCommand request, CancellationToken cancellationToken)
    {
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken)
            ?? throw new NotFoundException(nameof(Course), request.CourseId);

        var hasAccess = _currentUser.IsInRole(RoleNames.Owner)
            || (_currentUser.IsInRole(RoleNames.BranchManager) && _currentUser.HasAccessToBranch(course.BranchId))
            || (_currentUser.IsInRole(RoleNames.Teacher) && await _context.CourseSessions
                .AnyAsync(s => s.CourseId == request.CourseId && s.Teacher.UserId == _currentUser.UserId, cancellationToken));

        if (!hasAccess)
        {
            throw new ForbiddenAccessException("You do not have access to manage exams for this course.");
        }

        var exam = new Exam
        {
            Id = Guid.NewGuid(),
            CourseId = request.CourseId,
            Name = request.Name,
            MaxScore = request.MaxScore,
            ExamDate = request.ExamDate
        };

        _context.Exams.Add(exam);
        await _context.SaveChangesAsync(cancellationToken);

        return new ExamDto(exam.Id, exam.CourseId, exam.Name, exam.MaxScore, exam.ExamDate);
    }
}

using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Exams;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Exams.Commands.UpdateExam;

public class UpdateExamCommandHandler : IRequestHandler<UpdateExamCommand, ExamDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateExamCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ExamDto> Handle(UpdateExamCommand request, CancellationToken cancellationToken)
    {
        var exam = await _context.Exams
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Exam), request.Id);

        var hasAccess = _currentUser.IsInRole(RoleNames.Owner)
            || (_currentUser.IsInRole(RoleNames.BranchManager) && _currentUser.HasAccessToBranch(exam.Course.BranchId))
            || (_currentUser.IsInRole(RoleNames.Teacher) && await _context.CourseSessions
                .AnyAsync(s => s.CourseId == exam.CourseId && s.Teacher.UserId == _currentUser.UserId, cancellationToken));

        if (!hasAccess)
        {
            throw new ForbiddenAccessException("You do not have access to manage exams for this course.");
        }

        exam.Name = request.Name;
        exam.MaxScore = request.MaxScore;
        exam.ExamDate = request.ExamDate;

        await _context.SaveChangesAsync(cancellationToken);

        return new ExamDto(exam.Id, exam.CourseId, exam.Name, exam.MaxScore, exam.ExamDate);
    }
}

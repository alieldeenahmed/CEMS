using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Exams;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Exams.Queries.GetExamById;

public class GetExamByIdQueryHandler : IRequestHandler<GetExamByIdQuery, ExamDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetExamByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ExamDto> Handle(GetExamByIdQuery request, CancellationToken cancellationToken)
    {
        var exam = await _context.Exams
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Exam), request.Id);

        var hasAccess = _currentUser.HasAccessToBranch(exam.Course.BranchId)
            || await _context.CourseSessions.AnyAsync(s => s.CourseId == exam.CourseId && s.Teacher.UserId == _currentUser.UserId, cancellationToken);

        if (!hasAccess)
        {
            throw new ForbiddenAccessException("You do not have access to this exam.");
        }

        return new ExamDto(exam.Id, exam.CourseId, exam.Name, exam.MaxScore, exam.ExamDate);
    }
}

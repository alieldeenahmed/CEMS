using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Students;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Exams.Queries.GenerateReportCard;

public class GenerateReportCardQueryHandler : IRequestHandler<GenerateReportCardQuery, byte[]>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IReportCardGenerator _reportCardGenerator;

    public GenerateReportCardQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser, IReportCardGenerator reportCardGenerator)
    {
        _context = context;
        _currentUser = currentUser;
        _reportCardGenerator = reportCardGenerator;
    }

    public async Task<byte[]> Handle(GenerateReportCardQuery request, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Student), request.StudentId);

        var hasAccess = _currentUser.IsInRole(RoleNames.Owner) || _currentUser.HasAccessToBranch(student.CurrentBranchId);

        if (!hasAccess)
        {
            throw new ForbiddenAccessException("You do not have access to this student.");
        }

        var courseIds = await _context.CourseEnrollments
            .Where(e => e.StudentId == request.StudentId)
            .Select(e => e.CourseId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var courseNames = await _context.Courses
            .Where(c => courseIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var examRows = await _context.Exams
            .Where(e => courseIds.Contains(e.CourseId))
            .OrderBy(e => e.ExamDate)
            .Select(e => new
            {
                e.CourseId,
                e.Name,
                e.MaxScore,
                e.ExamDate,
                Score = e.Grades.Where(g => g.StudentId == request.StudentId).Select(g => (decimal?)g.Score).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var courseSections = examRows
            .GroupBy(row => row.CourseId)
            .Select(group => new ReportCardCourseSection(
                courseNames.GetValueOrDefault(group.Key, "Unknown Course"),
                group.Select(row => new ReportCardExamRow(row.Name, row.ExamDate, row.MaxScore, row.Score)).ToList()))
            .ToList();

        var data = new ReportCardData(student.FullName, courseSections);

        return _reportCardGenerator.Generate(data);
    }
}

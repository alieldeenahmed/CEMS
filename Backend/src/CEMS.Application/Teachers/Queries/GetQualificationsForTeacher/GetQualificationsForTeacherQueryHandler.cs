using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Teachers;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Teachers.Queries.GetQualificationsForTeacher;

public class GetQualificationsForTeacherQueryHandler : IRequestHandler<GetQualificationsForTeacherQuery, List<TeacherCourseQualificationDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetQualificationsForTeacherQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<TeacherCourseQualificationDto>> Handle(GetQualificationsForTeacherQuery request, CancellationToken cancellationToken)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == request.TeacherId, cancellationToken)
            ?? throw new NotFoundException(nameof(Teacher), request.TeacherId);

        var branchIds = await _context.TeacherBranches
            .Where(tb => tb.TeacherId == teacher.Id)
            .Select(tb => tb.BranchId)
            .ToListAsync(cancellationToken);

        var isOwnRecord = teacher.UserId == _currentUser.UserId;
        var hasAccess = _currentUser.IsInRole(RoleNames.Owner)
            || isOwnRecord
            || branchIds.Any(_currentUser.HasAccessToBranch);

        if (!hasAccess)
        {
            throw new ForbiddenAccessException("You do not have access to this teacher.");
        }

        return await _context.TeacherCourseQualifications
            .Where(q => q.TeacherId == request.TeacherId)
            .OrderBy(q => q.Course.Name)
            .Select(q => new TeacherCourseQualificationDto(q.TeacherId, q.CourseId, q.Course.Name))
            .ToListAsync(cancellationToken);
    }
}

using CEMS.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Scheduling.Queries.GetMySchedule;

public class GetMyScheduleQueryHandler : IRequestHandler<GetMyScheduleQuery, List<CourseSessionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyScheduleQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<CourseSessionDto>> Handle(GetMyScheduleQuery request, CancellationToken cancellationToken)
    {
        return await _context.CourseSessions
            .Where(s => s.Teacher.UserId == _currentUser.UserId)
            .OrderBy(s => s.StartUtc)
            .Select(s => new CourseSessionDto(
                s.Id, s.CourseId, s.RoomId, s.TeacherId, s.StartUtc, s.EndUtc,
                s.Status, s.Overridden, s.OverrideReason, s.RescheduledToSessionId))
            .ToListAsync(cancellationToken);
    }
}

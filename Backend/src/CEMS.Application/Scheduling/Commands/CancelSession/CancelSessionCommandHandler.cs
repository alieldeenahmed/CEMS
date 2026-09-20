using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Scheduling.Commands.CancelSession;

public class CancelSessionCommandHandler : IRequestHandler<CancelSessionCommand, CourseSessionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CancelSessionCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CourseSessionDto> Handle(CancelSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.CourseSessions
            .Include(s => s.Course)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CourseSession), request.Id);

        _currentUser.EnsureAccessToBranch(session.Course.BranchId);

        if (session.Status != SessionStatus.Scheduled)
        {
            throw new BadRequestException(new[] { $"Only a scheduled session can be cancelled; this one is {session.Status}." });
        }

        if (request.RescheduledToSessionId.HasValue)
        {
            // A replacement is a session of the same course; without this a manager could point a
            // cancelled session at any session in the system, including another branch's.
            var replacementIsValid = request.RescheduledToSessionId.Value != session.Id
                && await _context.CourseSessions.AnyAsync(
                    s => s.Id == request.RescheduledToSessionId.Value && s.CourseId == session.CourseId, cancellationToken);
            if (!replacementIsValid)
            {
                throw new BadRequestException(new[] { "The replacement must be a different session of the same course." });
            }
        }

        session.Status = SessionStatus.Cancelled;
        session.RescheduledToSessionId = request.RescheduledToSessionId;

        await _context.SaveChangesAsync(cancellationToken);

        return new CourseSessionDto(
            session.Id, session.CourseId, session.RoomId, session.TeacherId, session.StartUtc, session.EndUtc,
            session.Status, session.Overridden, session.OverrideReason, session.RescheduledToSessionId);
    }
}

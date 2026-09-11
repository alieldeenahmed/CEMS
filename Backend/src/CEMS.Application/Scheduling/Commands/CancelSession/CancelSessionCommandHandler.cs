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

        if (!_currentUser.HasAccessToBranch(session.Course.BranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }

        if (request.RescheduledToSessionId.HasValue)
        {
            var replacementExists = await _context.CourseSessions.AnyAsync(s => s.Id == request.RescheduledToSessionId.Value, cancellationToken);
            if (!replacementExists)
            {
                throw new NotFoundException(nameof(CourseSession), request.RescheduledToSessionId.Value);
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

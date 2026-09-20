using CEMS.Application.Common.Concurrency;
using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Branches;
using CEMS.Domain.Courses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Scheduling.Commands.CreateSession;

public class CreateSessionCommandHandler : IRequestHandler<CreateSessionCommand, CourseSessionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateSessionCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CourseSessionDto> Handle(CreateSessionCommand request, CancellationToken cancellationToken)
    {
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken)
            ?? throw new NotFoundException(nameof(Course), request.CourseId);

        _currentUser.EnsureAccessToBranch(course.BranchId);

        var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Id == request.RoomId, cancellationToken)
            ?? throw new NotFoundException(nameof(Room), request.RoomId);

        if (room.BranchId != course.BranchId)
        {
            throw new BadRequestException(new[] { "The room does not belong to the course's branch." });
        }

        await SessionRules.EnsureTeacherCanTeachAsync(_context, request.TeacherId, course.Id, course.BranchId, cancellationToken);

        // Check-then-insert is a race: two requests for the same room or teacher could both find the slot
        // free and both insert. Taking the room and teacher locks first makes the second request wait,
        // then see the first one's session when it runs its checks. (The overlap exclusion constraints in
        // the database are the backstop if a code path ever skips this.)
        await using var transaction = await _context.BeginLockedTransactionAsync(
            cancellationToken, LockKeys.Room(request.RoomId), LockKeys.Teacher(request.TeacherId));

        var conflicts = await SessionRules.FindConflictsAsync(
            _context, request.RoomId, request.TeacherId, course.BranchId, request.StartUtc, request.EndUtc, null, cancellationToken);

        SessionRules.EnsureConflictsMayProceed(_currentUser, conflicts, request.Override, request.OverrideReason);

        var hasConflicts = conflicts.Count > 0;

        var session = new CourseSession
        {
            Id = Guid.NewGuid(),
            CourseId = request.CourseId,
            RoomId = request.RoomId,
            TeacherId = request.TeacherId,
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc,
            Status = SessionStatus.Scheduled,
            Overridden = hasConflicts,
            OverrideReason = hasConflicts ? request.OverrideReason : null,
            OverriddenByUserId = hasConflicts ? _currentUser.UserId : null
        };

        _context.CourseSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CourseSessionDto(
            session.Id, session.CourseId, session.RoomId, session.TeacherId, session.StartUtc, session.EndUtc,
            session.Status, session.Overridden, session.OverrideReason, session.RescheduledToSessionId);
    }
}

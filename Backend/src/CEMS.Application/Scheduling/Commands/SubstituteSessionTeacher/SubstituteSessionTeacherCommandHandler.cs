using CEMS.Application.Common.Concurrency;
using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Scheduling.Commands.SubstituteSessionTeacher;

public class SubstituteSessionTeacherCommandHandler : IRequestHandler<SubstituteSessionTeacherCommand, CourseSessionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SubstituteSessionTeacherCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CourseSessionDto> Handle(SubstituteSessionTeacherCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.CourseSessions
            .Include(s => s.Course)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(CourseSession), request.SessionId);

        _currentUser.EnsureAccessToBranch(session.Course.BranchId);

        if (session.Status == SessionStatus.Cancelled)
        {
            throw new BadRequestException(new[] { "Cannot substitute the teacher for a cancelled session." });
        }

        if (session.StartUtc <= DateTime.UtcNow)
        {
            throw new BadRequestException(new[] { "Cannot substitute the teacher for a session that has already started." });
        }

        if (request.NewTeacherId == session.TeacherId)
        {
            throw new BadRequestException(new[] { "This teacher is already assigned to this session." });
        }

        await SessionRules.EnsureTeacherCanTeachAsync(
            _context, request.NewTeacherId, session.CourseId, session.Course.BranchId, cancellationToken);

        // Same race as creating a session: lock the incoming teacher so two concurrent bookings of them
        // are checked one after the other. The room is not changing, so it needs no lock.
        await using var transaction = await _context.BeginLockedTransactionAsync(
            cancellationToken, LockKeys.Teacher(request.NewTeacherId));

        // Only the new teacher's double-booking and availability need checking; the session being
        // reassigned is excluded from its own conflict check.
        var conflicts = await SessionRules.FindConflictsAsync(
            _context, null, request.NewTeacherId, session.Course.BranchId, session.StartUtc, session.EndUtc, session.Id, cancellationToken);

        SessionRules.EnsureConflictsMayProceed(_currentUser, conflicts, request.Override, request.OverrideReason);

        session.TeacherId = request.NewTeacherId;

        if (conflicts.Count > 0)
        {
            // Escalate, never erase: a session that was already booked by override keeps that record, and
            // a new override is added to it rather than replacing it.
            session.OverrideReason = AppendReason(session.OverrideReason, request.OverrideReason!);
            session.Overridden = true;
            session.OverriddenByUserId = _currentUser.UserId;
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CourseSessionDto(
            session.Id, session.CourseId, session.RoomId, session.TeacherId, session.StartUtc, session.EndUtc,
            session.Status, session.Overridden, session.OverrideReason, session.RescheduledToSessionId);
    }

    private static string AppendReason(string? existing, string added)
    {
        const int maxLength = 500;
        var combined = string.IsNullOrWhiteSpace(existing) ? added : $"{existing} | Substitution: {added}";
        return combined.Length <= maxLength ? combined : combined[..maxLength];
    }
}

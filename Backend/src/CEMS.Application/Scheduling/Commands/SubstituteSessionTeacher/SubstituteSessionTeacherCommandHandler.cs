using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using CEMS.Domain.Teachers;
using CEMS.Domain.Users;
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

        if (!_currentUser.HasAccessToBranch(session.Course.BranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }

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

        var teacherExists = await _context.Teachers.AnyAsync(t => t.Id == request.NewTeacherId, cancellationToken);
        if (!teacherExists)
        {
            throw new NotFoundException(nameof(Teacher), request.NewTeacherId);
        }

        var teacherAssignedToBranch = await _context.TeacherBranches
            .AnyAsync(tb => tb.TeacherId == request.NewTeacherId && tb.BranchId == session.Course.BranchId, cancellationToken);

        if (!teacherAssignedToBranch)
        {
            throw new BadRequestException(new[] { "This teacher is not assigned to the course's branch." });
        }

        var conflicts = new List<string>();

        // The room's booking for this slot is unaffected -- only the teacher is changing -- so only
        // the new teacher's double-booking and declared availability need checking, and the session
        // being reassigned is excluded from its own conflict check.
        var teacherConflict = await _context.CourseSessions.AnyAsync(
            s => s.Id != session.Id
                && s.TeacherId == request.NewTeacherId
                && s.Status != SessionStatus.Cancelled
                && s.StartUtc < session.EndUtc
                && session.StartUtc < s.EndUtc,
            cancellationToken);

        if (teacherConflict)
        {
            conflicts.Add("The teacher is already booked for an overlapping time slot.");
        }

        var sessionDayOfWeek = session.StartUtc.DayOfWeek;
        var sessionStartTime = TimeOnly.FromDateTime(session.StartUtc);
        var sessionEndTime = TimeOnly.FromDateTime(session.EndUtc);

        var isWithinAvailability = await _context.TeacherAvailabilities.AnyAsync(
            a => a.TeacherId == request.NewTeacherId
                && a.BranchId == session.Course.BranchId
                && a.DayOfWeek == sessionDayOfWeek
                && a.StartTime <= sessionStartTime
                && a.EndTime >= sessionEndTime,
            cancellationToken);

        if (!isWithinAvailability)
        {
            conflicts.Add("The session falls outside the teacher's declared availability for this branch.");
        }

        var hasConflicts = conflicts.Count > 0;

        if (hasConflicts && !request.Override)
        {
            throw new SchedulingConflictException(conflicts);
        }

        if (hasConflicts && request.Override)
        {
            if (!_currentUser.IsInRole(RoleNames.Owner) && !_currentUser.IsInRole(RoleNames.BranchManager))
            {
                throw new ForbiddenAccessException("Only Owner or BranchManager can override a scheduling conflict.");
            }

            if (string.IsNullOrWhiteSpace(request.OverrideReason))
            {
                throw new BadRequestException(new[] { "An override reason is required when overriding a scheduling conflict." });
            }
        }

        session.TeacherId = request.NewTeacherId;
        session.Overridden = hasConflicts;
        session.OverrideReason = hasConflicts ? request.OverrideReason : null;
        session.OverriddenByUserId = hasConflicts ? _currentUser.UserId : null;

        await _context.SaveChangesAsync(cancellationToken);

        return new CourseSessionDto(
            session.Id, session.CourseId, session.RoomId, session.TeacherId, session.StartUtc, session.EndUtc,
            session.Status, session.Overridden, session.OverrideReason, session.RescheduledToSessionId);
    }
}

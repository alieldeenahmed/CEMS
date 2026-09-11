using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Branches;
using CEMS.Domain.Courses;
using CEMS.Domain.Teachers;
using CEMS.Domain.Users;
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

        if (!_currentUser.HasAccessToBranch(course.BranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }

        var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Id == request.RoomId, cancellationToken)
            ?? throw new NotFoundException(nameof(Room), request.RoomId);

        if (room.BranchId != course.BranchId)
        {
            throw new BadRequestException(new[] { "The room does not belong to the course's branch." });
        }

        var teacherExists = await _context.Teachers.AnyAsync(t => t.Id == request.TeacherId, cancellationToken);
        if (!teacherExists)
        {
            throw new NotFoundException(nameof(Teacher), request.TeacherId);
        }

        var teacherAssignedToBranch = await _context.TeacherBranches
            .AnyAsync(tb => tb.TeacherId == request.TeacherId && tb.BranchId == course.BranchId, cancellationToken);

        if (!teacherAssignedToBranch)
        {
            throw new BadRequestException(new[] { "This teacher is not assigned to the course's branch." });
        }

        var conflicts = new List<string>();

        var roomConflict = await _context.CourseSessions.AnyAsync(
            s => s.RoomId == request.RoomId
                && s.Status != SessionStatus.Cancelled
                && s.StartUtc < request.EndUtc
                && request.StartUtc < s.EndUtc,
            cancellationToken);

        if (roomConflict)
        {
            conflicts.Add("The room is already booked for an overlapping time slot.");
        }

        var teacherConflict = await _context.CourseSessions.AnyAsync(
            s => s.TeacherId == request.TeacherId
                && s.Status != SessionStatus.Cancelled
                && s.StartUtc < request.EndUtc
                && request.StartUtc < s.EndUtc,
            cancellationToken);

        if (teacherConflict)
        {
            conflicts.Add("The teacher is already booked for an overlapping time slot.");
        }

        var sessionDayOfWeek = request.StartUtc.DayOfWeek;
        var sessionStartTime = TimeOnly.FromDateTime(request.StartUtc);
        var sessionEndTime = TimeOnly.FromDateTime(request.EndUtc);

        var isWithinAvailability = await _context.TeacherAvailabilities.AnyAsync(
            a => a.TeacherId == request.TeacherId
                && a.BranchId == course.BranchId
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

        return new CourseSessionDto(
            session.Id, session.CourseId, session.RoomId, session.TeacherId, session.StartUtc, session.EndUtc,
            session.Status, session.Overridden, session.OverrideReason, session.RescheduledToSessionId);
    }
}

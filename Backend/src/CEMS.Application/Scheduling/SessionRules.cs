using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using CEMS.Domain.Teachers;
using CEMS.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Scheduling;

/// <summary>
/// The rules shared by creating a session and substituting its teacher, so the two paths cannot drift.
///
/// Two kinds of rule, deliberately handled differently:
///  * Structural rules (the teacher exists, works at the course's branch, is qualified for the course)
///    describe things that are simply not valid; they are never overridable.
///  * Conflict rules (room double-booked, teacher double-booked, outside declared availability) describe
///    things that are valid but collide with something; an Owner or BranchManager may override them with
///    a recorded reason.
/// </summary>
internal static class SessionRules
{
    public const string RoomBooked = "The room is already booked for an overlapping time slot.";
    public const string TeacherBooked = "The teacher is already booked for an overlapping time slot.";
    public const string OutsideAvailability = "The session falls outside the teacher's declared availability for this branch.";

    public static async Task EnsureTeacherCanTeachAsync(
        IApplicationDbContext context, Guid teacherId, Guid courseId, Guid branchId, CancellationToken cancellationToken)
    {
        if (!await context.Teachers.AnyAsync(t => t.Id == teacherId, cancellationToken))
        {
            throw new NotFoundException(nameof(Teacher), teacherId);
        }

        if (!await context.TeacherBranches.AnyAsync(tb => tb.TeacherId == teacherId && tb.BranchId == branchId, cancellationToken))
        {
            throw new BadRequestException(new[] { "This teacher is not assigned to the course's branch." });
        }

        if (!await context.TeacherCourseQualifications.AnyAsync(q => q.TeacherId == teacherId && q.CourseId == courseId, cancellationToken))
        {
            throw new BadRequestException(new[] { "This teacher is not qualified to teach this course." });
        }
    }

    /// <summary>
    /// Finds every conflict for the slot. Pass <paramref name="roomId"/> as null when the room is not
    /// changing (a substitution), and the session's own id as <paramref name="ignoreSessionId"/> so it is
    /// not compared against itself.
    /// </summary>
    public static async Task<List<string>> FindConflictsAsync(
        IApplicationDbContext context, Guid? roomId, Guid teacherId, Guid branchId,
        DateTime startUtc, DateTime endUtc, Guid? ignoreSessionId, CancellationToken cancellationToken)
    {
        var conflicts = new List<string>();

        if (roomId.HasValue)
        {
            var roomBooked = await context.CourseSessions.AnyAsync(
                s => s.Id != ignoreSessionId
                    && s.RoomId == roomId.Value
                    && s.Status != SessionStatus.Cancelled
                    && s.StartUtc < endUtc
                    && startUtc < s.EndUtc,
                cancellationToken);

            if (roomBooked)
            {
                conflicts.Add(RoomBooked);
            }
        }

        var teacherBooked = await context.CourseSessions.AnyAsync(
            s => s.Id != ignoreSessionId
                && s.TeacherId == teacherId
                && s.Status != SessionStatus.Cancelled
                && s.StartUtc < endUtc
                && startUtc < s.EndUtc,
            cancellationToken);

        if (teacherBooked)
        {
            conflicts.Add(TeacherBooked);
        }

        // Availability is a weekly day + time-of-day window, so it can only describe a session that starts
        // and ends on the same (UTC) day. Comparing time-of-day alone would let an overnight session slip
        // through -- 22:00 to 00:30 has an "end time" earlier than any window's end.
        var day = startUtc.DayOfWeek;
        var start = TimeOnly.FromDateTime(startUtc);
        var end = TimeOnly.FromDateTime(endUtc);
        var withinAvailability = startUtc.Date == endUtc.Date
            && await context.TeacherAvailabilities.AnyAsync(
                a => a.TeacherId == teacherId
                    && a.BranchId == branchId
                    && a.DayOfWeek == day
                    && a.StartTime <= start
                    && a.EndTime >= end,
                cancellationToken);

        if (!withinAvailability)
        {
            conflicts.Add(OutsideAvailability);
        }

        return conflicts;
    }

    /// <summary>Throws unless the caller may (and properly did) override the conflicts found.</summary>
    public static void EnsureConflictsMayProceed(
        ICurrentUserService currentUser, IReadOnlyList<string> conflicts, bool overrideRequested, string? overrideReason)
    {
        if (conflicts.Count == 0)
        {
            return;
        }

        if (!overrideRequested)
        {
            throw new SchedulingConflictException(conflicts);
        }

        if (!currentUser.IsInRole(RoleNames.Owner) && !currentUser.IsInRole(RoleNames.BranchManager))
        {
            throw new ForbiddenAccessException("Only Owner or BranchManager can override a scheduling conflict.");
        }

        if (string.IsNullOrWhiteSpace(overrideReason))
        {
            throw new BadRequestException(new[] { "An override reason is required when overriding a scheduling conflict." });
        }
    }
}

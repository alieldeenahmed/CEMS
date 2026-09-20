using CEMS.Application.Common.Exceptions;
using CEMS.Application.Scheduling.Commands.CancelSession;
using CEMS.Application.Scheduling.Commands.CreateSession;
using CEMS.Application.Scheduling.Commands.SubstituteSessionTeacher;
using CEMS.Application.Tests.TestSupport;
using CEMS.Domain.Branches;
using CEMS.Domain.Courses;
using CEMS.Domain.Teachers;
using CEMS.Domain.Users;

namespace CEMS.Application.Tests.Scheduling;

/// <summary>
/// The rules shared by creating a session and substituting its teacher: the qualification requirement
/// (structural, never overridable), overnight sessions, and preserving the override audit trail.
/// </summary>
public class SchedulingRulesTests : SeededHandlerTestBase
{
    private static readonly DateTime Monday10 = DateTime.SpecifyKind(new DateTime(2030, 1, 7, 10, 0, 0), DateTimeKind.Utc);

    private readonly Branch _smouha;
    private readonly Room _room;
    private readonly Course _course;

    public SchedulingRulesTests()
    {
        _smouha = AddBranch("Smouha");
        _room = AddRoom(_smouha);
        _course = AddCourse(_smouha);
    }

    private Teacher AddQualifiedTeacher(bool qualified = true, bool available = true)
    {
        var teacher = AddTeacher(_smouha);
        if (qualified)
        {
            Context.TeacherCourseQualifications.Add(new TeacherCourseQualification { TeacherId = teacher.Id, CourseId = _course.Id });
        }

        if (available)
        {
            Context.TeacherAvailabilities.Add(new TeacherAvailability
            {
                Id = Guid.NewGuid(), TeacherId = teacher.Id, BranchId = _smouha.Id, DayOfWeek = DayOfWeek.Monday,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0)
            });
        }

        Context.SaveChanges();
        return teacher;
    }

    private Task<CEMS.Application.Scheduling.CourseSessionDto> Create(Teacher teacher, DateTime? start = null, DateTime? end = null, bool @override = false, string? reason = null) =>
        new CreateSessionCommandHandler(Context, CurrentUser).Handle(
            new CreateSessionCommand(_course.Id, _room.Id, teacher.Id, start ?? Monday10, end ?? (start ?? Monday10).AddHours(1), @override, reason),
            CancellationToken.None);

    // ---- Qualification ----

    [Fact]
    public async Task CreateSession_ForATeacherNotQualifiedForTheCourse_IsRejected()
    {
        var unqualified = AddQualifiedTeacher(qualified: false);
        ActAs(RoleNames.Owner, _smouha);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => Create(unqualified));

        Assert.Contains("not qualified", string.Join(' ', ex.Errors));
        Assert.Empty(Context.CourseSessions);
    }

    [Theory]
    [InlineData(RoleNames.Owner)]
    [InlineData(RoleNames.BranchManager)]
    public async Task Qualification_IsStructural_SoNoOverrideCanWaiveIt(string role)
    {
        var unqualified = AddQualifiedTeacher(qualified: false);
        ActAs(role, _smouha);

        await Assert.ThrowsAsync<BadRequestException>(() => Create(unqualified, @override: true, reason: "Nobody else is free"));

        Assert.Empty(Context.CourseSessions);
    }

    [Fact]
    public async Task Qualification_IsPerCourse_QualifiedForAnotherCourseIsNotEnough()
    {
        var teacher = AddTeacher(_smouha);
        var otherCourse = AddCourse(_smouha, name: "Robotics");
        Context.TeacherCourseQualifications.Add(new TeacherCourseQualification { TeacherId = teacher.Id, CourseId = otherCourse.Id });
        Context.SaveChanges();
        ActAs(RoleNames.Owner, _smouha);

        await Assert.ThrowsAsync<BadRequestException>(() => Create(teacher));
    }

    [Fact]
    public async Task CreateSession_ForAQualifiedTeacher_Succeeds()
    {
        var teacher = AddQualifiedTeacher();
        ActAs(RoleNames.FrontDesk, _smouha);

        var session = await Create(teacher);

        Assert.Equal(teacher.Id, session.TeacherId);
        Assert.False(session.Overridden);
    }

    [Fact]
    public async Task Substitute_ToATeacherNotQualifiedForTheCourse_IsRejectedEvenWithAnOverride()
    {
        var original = AddQualifiedTeacher();
        var unqualified = AddQualifiedTeacher(qualified: false);
        var session = AddSession(_course, _room, original, DateTime.UtcNow.AddDays(3));
        ActAs(RoleNames.Owner, _smouha);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            new SubstituteSessionTeacherCommandHandler(Context, CurrentUser).Handle(
                new SubstituteSessionTeacherCommand(session.Id, unqualified.Id, true, "Emergency cover"), CancellationToken.None));

        Assert.Contains("not qualified", string.Join(' ', ex.Errors));
        Context.ChangeTracker.Clear();
        Assert.Equal(original.Id, Context.CourseSessions.Single().TeacherId);
    }

    [Fact]
    public async Task RemovingAQualification_StopsFutureBookings_ButLeavesExistingSessionsAlone()
    {
        var teacher = AddQualifiedTeacher();
        ActAs(RoleNames.Owner, _smouha);
        await Create(teacher);

        Context.TeacherCourseQualifications.RemoveRange(Context.TeacherCourseQualifications);
        Context.SaveChanges();

        await Assert.ThrowsAsync<BadRequestException>(() => Create(teacher, start: Monday10.AddDays(7)));
        Assert.Single(Context.CourseSessions);
    }

    // ---- Overnight sessions ----

    [Fact]
    public async Task AnOvernightSession_IsNeverWithinADaytimeAvailabilityWindow()
    {
        var teacher = AddQualifiedTeacher();   // available Monday 09:00-17:00
        ActAs(RoleNames.FrontDesk, _smouha);

        // 16:00 Monday to 00:30 Tuesday: compared by time-of-day alone, its "end" (00:30) would look like it
        // fits inside the window.
        var ex = await Assert.ThrowsAsync<SchedulingConflictException>(() =>
            Create(teacher, start: Monday10.AddHours(6), end: Monday10.AddHours(14).AddMinutes(30)));

        Assert.Contains(ex.Conflicts, c => c.Contains("availability"));
    }

    // ---- Substitution keeps the audit trail ----

    [Fact]
    public async Task Substitute_CleanlyOntoAnOverriddenSession_DoesNotEraseTheOriginalOverride()
    {
        var original = AddQualifiedTeacher();
        var substitute = AddQualifiedTeacher();
        var session = AddSession(_course, _room, original, DateTime.UtcNow.AddDays(7));
        session.Overridden = true;
        session.OverrideReason = "Room shared with the open day";
        session.OverriddenByUserId = Guid.NewGuid();
        Context.SaveChanges();
        ActAs(RoleNames.BranchManager, _smouha);

        // Pick a Monday inside the substitute's availability so the substitution itself is conflict-free.
        session.StartUtc = NextMonday10();
        session.EndUtc = session.StartUtc.AddHours(1);
        Context.SaveChanges();

        var result = await new SubstituteSessionTeacherCommandHandler(Context, CurrentUser).Handle(
            new SubstituteSessionTeacherCommand(session.Id, substitute.Id, false, null), CancellationToken.None);

        Assert.Equal(substitute.Id, result.TeacherId);
        Assert.True(result.Overridden);
        Assert.Equal("Room shared with the open day", result.OverrideReason);
    }

    [Fact]
    public async Task Substitute_WithANewConflict_AddsToTheExistingOverrideReason()
    {
        var original = AddQualifiedTeacher();
        var substitute = AddQualifiedTeacher(available: false);
        var session = AddSession(_course, _room, original, NextMonday10());
        session.Overridden = true;
        session.OverrideReason = "Room shared with the open day";
        Context.SaveChanges();
        ActAs(RoleNames.BranchManager, _smouha);

        var result = await new SubstituteSessionTeacherCommandHandler(Context, CurrentUser).Handle(
            new SubstituteSessionTeacherCommand(session.Id, substitute.Id, true, "Covering for illness"), CancellationToken.None);

        Assert.True(result.Overridden);
        Assert.Equal("Room shared with the open day | Substitution: Covering for illness", result.OverrideReason);
    }

    // ---- Cancelling ----

    [Fact]
    public async Task Cancel_ASessionThatIsAlreadyCancelled_IsRejected()
    {
        var teacher = AddQualifiedTeacher();
        var session = AddSession(_course, _room, teacher, Monday10, status: SessionStatus.Cancelled);
        ActAs(RoleNames.Owner);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            new CancelSessionCommandHandler(Context, CurrentUser).Handle(new CancelSessionCommand(session.Id, null), CancellationToken.None));
    }

    [Fact]
    public async Task Cancel_ACompletedSession_IsRejected()
    {
        var teacher = AddQualifiedTeacher();
        var session = AddSession(_course, _room, teacher, Monday10, status: SessionStatus.Completed);
        ActAs(RoleNames.Owner);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            new CancelSessionCommandHandler(Context, CurrentUser).Handle(new CancelSessionCommand(session.Id, null), CancellationToken.None));
    }

    [Fact]
    public async Task Cancel_TheReplacementMustBeAnotherSessionOfTheSameCourse()
    {
        var teacher = AddQualifiedTeacher();
        var session = AddSession(_course, _room, teacher, Monday10);
        var otherCourse = AddCourse(_smouha, name: "Robotics");
        var otherCourseSession = AddSession(otherCourse, _room, teacher, Monday10.AddDays(1));
        ActAs(RoleNames.Owner);
        var handler = new CancelSessionCommandHandler(Context, CurrentUser);

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(new CancelSessionCommand(session.Id, otherCourseSession.Id), CancellationToken.None));
        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(new CancelSessionCommand(session.Id, session.Id), CancellationToken.None));

        Context.ChangeTracker.Clear();
        Assert.Equal(SessionStatus.Scheduled, Context.CourseSessions.Single(s => s.Id == session.Id).Status);
    }

    // The next Monday 10:00 UTC after today (always in the future, always inside a Monday window).
    private static DateTime NextMonday10()
    {
        var day = DateTime.UtcNow.Date.AddDays(1);
        while (day.DayOfWeek != DayOfWeek.Monday)
        {
            day = day.AddDays(1);
        }

        return DateTime.SpecifyKind(day.AddHours(10), DateTimeKind.Utc);
    }
}

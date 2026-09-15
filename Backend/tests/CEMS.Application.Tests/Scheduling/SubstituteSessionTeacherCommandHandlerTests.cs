using CEMS.Application.Common.Exceptions;
using CEMS.Application.Scheduling.Commands.SubstituteSessionTeacher;
using CEMS.Application.Tests.TestSupport;
using CEMS.Domain.Branches;
using CEMS.Domain.Courses;
using CEMS.Domain.Teachers;
using CEMS.Domain.Users;

namespace CEMS.Application.Tests.Scheduling;

public class SubstituteSessionTeacherCommandHandlerTests : HandlerTestBase
{
    private Guid _branchId;
    private Guid _originalTeacherId;
    private CourseSession _session = null!;

    private Teacher SeedNewTeacher(bool withMatchingAvailability)
    {
        var newTeacher = new Teacher { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), HireDate = DateOnly.FromDateTime(DateTime.UtcNow), PayType = PayType.Hourly, PayRate = 100 };
        Context.Teachers.Add(newTeacher);
        Context.TeacherBranches.Add(new TeacherBranch { TeacherId = newTeacher.Id, BranchId = _branchId });

        if (withMatchingAvailability)
        {
            Context.TeacherAvailabilities.Add(new TeacherAvailability
            {
                Id = Guid.NewGuid(),
                TeacherId = newTeacher.Id,
                BranchId = _branchId,
                DayOfWeek = _session.StartUtc.DayOfWeek,
                StartTime = TimeOnly.FromDateTime(_session.StartUtc),
                EndTime = TimeOnly.FromDateTime(_session.EndUtc)
            });
        }

        Context.SaveChanges();
        return newTeacher;
    }

    private void SeedSessionInFuture(SessionStatus status = SessionStatus.Scheduled)
    {
        var branch = new Branch { Id = Guid.NewGuid(), Name = "Downtown" };
        var room = new Room { Id = Guid.NewGuid(), Name = "Room A", Capacity = 10, BranchId = branch.Id };
        var originalTeacher = new Teacher { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), HireDate = DateOnly.FromDateTime(DateTime.UtcNow), PayType = PayType.Hourly, PayRate = 100 };
        var curriculum = new Curriculum { Id = Guid.NewGuid(), Name = "IG", Description = "IG" };
        var course = new Course { Id = Guid.NewGuid(), Name = "Math", DeliveryMode = DeliveryMode.Group, CurriculumId = curriculum.Id, BranchId = branch.Id };

        var start = DateTime.UtcNow.AddDays(7);
        var session = new CourseSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RoomId = room.Id,
            TeacherId = originalTeacher.Id,
            StartUtc = start,
            EndUtc = start.AddHours(1),
            Status = status
        };

        Context.AddRange(branch, room, originalTeacher, curriculum, course, session);
        Context.SaveChanges();

        _branchId = branch.Id;
        _originalTeacherId = originalTeacher.Id;
        _session = session;

        CurrentUser.UserId = Guid.NewGuid();
        CurrentUser.Roles = [RoleNames.FrontDesk];
        CurrentUser.BranchIds = [branch.Id];
    }

    private SubstituteSessionTeacherCommandHandler CreateHandler() => new(Context, CurrentUser);

    [Fact]
    public async Task Handle_NewTeacherWithMatchingAvailability_SucceedsWithNoOverride()
    {
        SeedSessionInFuture();
        var newTeacher = SeedNewTeacher(withMatchingAvailability: true);

        var handler = CreateHandler();
        var result = await handler.Handle(new SubstituteSessionTeacherCommand(_session.Id, newTeacher.Id, false, null), CancellationToken.None);

        Assert.Equal(newTeacher.Id, result.TeacherId);
        Assert.False(result.Overridden);
    }

    [Fact]
    public async Task Handle_NewTeacherWithoutAvailability_NoOverride_ThrowsSchedulingConflict()
    {
        SeedSessionInFuture();
        var newTeacher = SeedNewTeacher(withMatchingAvailability: false);

        var handler = CreateHandler();
        var exception = await Assert.ThrowsAsync<SchedulingConflictException>(
            () => handler.Handle(new SubstituteSessionTeacherCommand(_session.Id, newTeacher.Id, false, null), CancellationToken.None));

        Assert.Contains(exception.Conflicts, c => c.Contains("availability"));
    }

    [Fact]
    public async Task Handle_ConflictWithOverride_AsBranchManager_Succeeds()
    {
        SeedSessionInFuture();
        var newTeacher = SeedNewTeacher(withMatchingAvailability: false);
        CurrentUser.Roles = [RoleNames.BranchManager];

        var handler = CreateHandler();
        var result = await handler.Handle(new SubstituteSessionTeacherCommand(_session.Id, newTeacher.Id, true, "Covering for illness"), CancellationToken.None);

        Assert.Equal(newTeacher.Id, result.TeacherId);
        Assert.True(result.Overridden);
        Assert.Equal("Covering for illness", result.OverrideReason);
    }

    [Fact]
    public async Task Handle_ConflictWithOverride_AsFrontDesk_ThrowsForbidden()
    {
        SeedSessionInFuture(); // CurrentUser is FrontDesk by default in SeedSessionInFuture
        var newTeacher = SeedNewTeacher(withMatchingAvailability: false);

        var handler = CreateHandler();
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => handler.Handle(new SubstituteSessionTeacherCommand(_session.Id, newTeacher.Id, true, "Covering for illness"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CancelledSession_ThrowsBadRequest()
    {
        SeedSessionInFuture(SessionStatus.Cancelled);
        var newTeacher = SeedNewTeacher(withMatchingAvailability: true);

        var handler = CreateHandler();
        await Assert.ThrowsAsync<BadRequestException>(
            () => handler.Handle(new SubstituteSessionTeacherCommand(_session.Id, newTeacher.Id, false, null), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SessionAlreadyStarted_ThrowsBadRequest()
    {
        SeedSessionInFuture();
        _session.StartUtc = DateTime.UtcNow.AddHours(-1);
        Context.SaveChanges();
        var newTeacher = SeedNewTeacher(withMatchingAvailability: true);

        var handler = CreateHandler();
        await Assert.ThrowsAsync<BadRequestException>(
            () => handler.Handle(new SubstituteSessionTeacherCommand(_session.Id, newTeacher.Id, false, null), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SameTeacherAlreadyAssigned_ThrowsBadRequest()
    {
        SeedSessionInFuture();

        var handler = CreateHandler();
        await Assert.ThrowsAsync<BadRequestException>(
            () => handler.Handle(new SubstituteSessionTeacherCommand(_session.Id, _originalTeacherId, false, null), CancellationToken.None));
    }
}

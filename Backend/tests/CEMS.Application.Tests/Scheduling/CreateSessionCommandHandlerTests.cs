using CEMS.Application.Common.Exceptions;
using CEMS.Application.Scheduling.Commands.CreateSession;
using CEMS.Application.Tests.TestSupport;
using CEMS.Domain.Branches;
using CEMS.Domain.Courses;
using CEMS.Domain.Teachers;
using CEMS.Domain.Users;

namespace CEMS.Application.Tests.Scheduling;

public class CreateSessionCommandHandlerTests : HandlerTestBase
{
    // A fixed Monday, so availability windows don't depend on when the suite runs.
    private static readonly DateTime Start = new(2030, 1, 7, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = Start.AddHours(1);

    private Branch _branch = null!;
    private Room _room = null!;
    private Teacher _teacher = null!;
    private Course _course = null!;

    private Teacher AddTeacher(Branch branch, bool withAvailability = true)
    {
        var teacher = new Teacher { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), HireDate = new DateOnly(2024, 1, 1), PayType = PayType.Hourly, PayRate = 100 };
        Context.Teachers.Add(teacher);
        Context.TeacherBranches.Add(new TeacherBranch { TeacherId = teacher.Id, BranchId = branch.Id });

        if (withAvailability)
        {
            Context.TeacherAvailabilities.Add(new TeacherAvailability
            {
                Id = Guid.NewGuid(),
                TeacherId = teacher.Id,
                BranchId = branch.Id,
                DayOfWeek = Start.DayOfWeek,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(17, 0)
            });
        }

        return teacher;
    }

    private void Seed(string role = "Owner")
    {
        _branch = new Branch { Id = Guid.NewGuid(), Name = "Smouha" };
        _room = new Room { Id = Guid.NewGuid(), Name = "Lab 1", Capacity = 15, BranchId = _branch.Id };
        var curriculum = new Curriculum { Id = Guid.NewGuid(), Name = "Web", Description = "Web" };
        _course = new Course { Id = Guid.NewGuid(), Name = "Python", DeliveryMode = DeliveryMode.Group, CurriculumId = curriculum.Id, BranchId = _branch.Id };

        Context.AddRange(_branch, _room, curriculum, _course);
        _teacher = AddTeacher(_branch);
        Context.SaveChanges();

        CurrentUser.UserId = Guid.NewGuid();
        CurrentUser.Roles = [role];
        CurrentUser.BranchIds = [_branch.Id];
    }

    private CourseSession AddExistingSession(Teacher teacher, Room room, SessionStatus status = SessionStatus.Scheduled)
    {
        var session = new CourseSession
        {
            Id = Guid.NewGuid(),
            CourseId = _course.Id,
            RoomId = room.Id,
            TeacherId = teacher.Id,
            StartUtc = Start.AddMinutes(30),
            EndUtc = End.AddMinutes(30),
            Status = status
        };
        Context.CourseSessions.Add(session);
        Context.SaveChanges();
        return session;
    }

    private CreateSessionCommandHandler CreateHandler() => new(Context, CurrentUser);

    private CreateSessionCommand Command(Guid? roomId = null, Guid? teacherId = null, bool @override = false, string? reason = null) =>
        new(_course.Id, roomId ?? _room.Id, teacherId ?? _teacher.Id, Start, End, @override, reason);

    [Fact]
    public async Task Handle_NoConflicts_CreatesSessionWithoutOverride()
    {
        Seed();

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.Equal(SessionStatus.Scheduled, result.Status);
        Assert.False(result.Overridden);
        Assert.Null(result.OverrideReason);
    }

    [Fact]
    public async Task Handle_RoomAlreadyBooked_ThrowsSchedulingConflict()
    {
        Seed();
        var otherTeacher = AddTeacher(_branch);
        Context.SaveChanges();
        AddExistingSession(otherTeacher, _room);

        var ex = await Assert.ThrowsAsync<SchedulingConflictException>(
            () => CreateHandler().Handle(Command(), CancellationToken.None));

        Assert.Single(ex.Conflicts);
        Assert.Contains("room", ex.Conflicts[0]);
    }

    [Fact]
    public async Task Handle_TeacherAlreadyBooked_ThrowsSchedulingConflict()
    {
        Seed();
        var otherRoom = new Room { Id = Guid.NewGuid(), Name = "Lab 2", Capacity = 5, BranchId = _branch.Id };
        Context.Rooms.Add(otherRoom);
        Context.SaveChanges();
        AddExistingSession(_teacher, otherRoom);

        var ex = await Assert.ThrowsAsync<SchedulingConflictException>(
            () => CreateHandler().Handle(Command(), CancellationToken.None));

        Assert.Single(ex.Conflicts);
        Assert.Contains("teacher is already booked", ex.Conflicts[0]);
    }

    [Fact]
    public async Task Handle_CancelledSessionInSameSlot_IsNotAConflict()
    {
        Seed();
        AddExistingSession(_teacher, _room, SessionStatus.Cancelled);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.False(result.Overridden);
    }

    [Fact]
    public async Task Handle_BackToBackSessions_AreNotAConflict()
    {
        Seed();
        Context.CourseSessions.Add(new CourseSession
        {
            Id = Guid.NewGuid(), CourseId = _course.Id, RoomId = _room.Id, TeacherId = _teacher.Id,
            StartUtc = Start.AddHours(-1), EndUtc = Start, Status = SessionStatus.Scheduled
        });
        Context.SaveChanges();

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.False(result.Overridden);
    }

    [Fact]
    public async Task Handle_OutsideDeclaredAvailability_ThrowsSchedulingConflict()
    {
        Seed();
        var unavailable = AddTeacher(_branch, withAvailability: false);
        Context.SaveChanges();

        var ex = await Assert.ThrowsAsync<SchedulingConflictException>(
            () => CreateHandler().Handle(Command(teacherId: unavailable.Id), CancellationToken.None));

        Assert.Contains("availability", ex.Conflicts[0]);
    }

    [Fact]
    public async Task Handle_AllThreeConflicts_AreReportedTogether()
    {
        Seed();
        var unavailable = AddTeacher(_branch, withAvailability: false);
        Context.SaveChanges();
        AddExistingSession(unavailable, _room);

        var ex = await Assert.ThrowsAsync<SchedulingConflictException>(
            () => CreateHandler().Handle(Command(teacherId: unavailable.Id), CancellationToken.None));

        Assert.Equal(3, ex.Conflicts.Count);
    }

    [Fact]
    public async Task Handle_OwnerOverridesWithReason_PersistsAuditTrail()
    {
        Seed(RoleNames.Owner);
        var unavailable = AddTeacher(_branch, withAvailability: false);
        Context.SaveChanges();

        var result = await CreateHandler().Handle(
            Command(teacherId: unavailable.Id, @override: true, reason: "Covering an absence"), CancellationToken.None);

        Assert.True(result.Overridden);
        Assert.Equal("Covering an absence", result.OverrideReason);

        var stored = Context.CourseSessions.Single(s => s.Id == result.Id);
        Assert.Equal(CurrentUser.UserId, stored.OverriddenByUserId);
    }

    [Fact]
    public async Task Handle_OverrideWithoutReason_ThrowsBadRequest()
    {
        Seed(RoleNames.BranchManager);
        var unavailable = AddTeacher(_branch, withAvailability: false);
        Context.SaveChanges();

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => CreateHandler().Handle(Command(teacherId: unavailable.Id, @override: true, reason: "  "), CancellationToken.None));

        Assert.Contains("reason", ex.Errors[0]);
    }

    [Fact]
    public async Task Handle_FrontDeskCannotOverride()
    {
        Seed(RoleNames.FrontDesk);
        var unavailable = AddTeacher(_branch, withAvailability: false);
        Context.SaveChanges();

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => CreateHandler().Handle(Command(teacherId: unavailable.Id, @override: true, reason: "Because"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_OverrideFlagWithNoConflict_DoesNotMarkSessionOverridden()
    {
        Seed(RoleNames.FrontDesk);

        var result = await CreateHandler().Handle(Command(@override: true, reason: "Unneeded"), CancellationToken.None);

        Assert.False(result.Overridden);
        Assert.Null(result.OverrideReason);
    }

    [Fact]
    public async Task Handle_RoomFromAnotherBranch_ThrowsBadRequestEvenWithOverride()
    {
        Seed(RoleNames.Owner);
        var otherBranch = new Branch { Id = Guid.NewGuid(), Name = "Kafr Abdo" };
        var foreignRoom = new Room { Id = Guid.NewGuid(), Name = "Lab X", Capacity = 10, BranchId = otherBranch.Id };
        Context.AddRange(otherBranch, foreignRoom);
        Context.SaveChanges();

        await Assert.ThrowsAsync<BadRequestException>(
            () => CreateHandler().Handle(Command(roomId: foreignRoom.Id, @override: true, reason: "Because"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_TeacherNotAssignedToBranch_ThrowsBadRequestEvenWithOverride()
    {
        Seed(RoleNames.Owner);
        var otherBranch = new Branch { Id = Guid.NewGuid(), Name = "Kafr Abdo" };
        Context.Branches.Add(otherBranch);
        var outsider = AddTeacher(otherBranch);
        Context.SaveChanges();

        await Assert.ThrowsAsync<BadRequestException>(
            () => CreateHandler().Handle(Command(teacherId: outsider.Id, @override: true, reason: "Because"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ManagerOfAnotherBranch_ThrowsForbidden()
    {
        Seed(RoleNames.BranchManager);
        CurrentUser.BranchIds = [Guid.NewGuid()];

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => CreateHandler().Handle(Command(), CancellationToken.None));
    }
}

using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Application.Scheduling.Queries.GetMySchedule;
using CEMS.Application.Teachers.Commands.AddAvailability;
using CEMS.Application.Teachers.Commands.AddTeacherQualification;
using CEMS.Application.Teachers.Commands.AddTeacherToBranch;
using CEMS.Application.Teachers.Commands.CreateTeacher;
using CEMS.Application.Teachers.Commands.RemoveAvailability;
using CEMS.Application.Teachers.Commands.RemoveTeacherFromBranch;
using CEMS.Application.Teachers.Commands.RemoveTeacherQualification;
using CEMS.Application.Teachers.Commands.UpdateTeacher;
using CEMS.Application.Teachers.Queries.GetAvailabilityForTeacher;
using CEMS.Application.Teachers.Queries.GetMyTeacherProfile;
using CEMS.Application.Teachers.Queries.GetQualificationsForTeacher;
using CEMS.Application.Teachers.Queries.GetTeacherById;
using CEMS.Application.Teachers.Queries.GetTeacherCandidates;
using CEMS.Application.Teachers.Queries.GetTeachers;
using CEMS.Application.Tests.TestSupport;
using CEMS.Domain.Branches;
using CEMS.Domain.Teachers;
using CEMS.Domain.Users;

namespace CEMS.Application.Tests.Teachers;

public class TeacherHandlerTests : SeededHandlerTestBase
{
    private readonly TestIdentityService _identity = new();
    private readonly Branch _smouha;
    private readonly Branch _kafrAbdo;

    public TeacherHandlerTests()
    {
        _smouha = AddBranch("Smouha");
        _kafrAbdo = AddBranch("Kafr Abdo");
    }

    /// <summary>A teacher with both a profile row and a matching identity account.</summary>
    private Teacher AddTeacherWithAccount(string name, params Branch[] branches)
    {
        var teacher = AddTeacher();
        foreach (var branch in branches)
        {
            Context.TeacherBranches.Add(new TeacherBranch { TeacherId = teacher.Id, BranchId = branch.Id });
        }

        Context.SaveChanges();
        _identity.AddUser(new AuthenticatedUser(teacher.UserId, $"{name.ToLower()}@codecamp.demo", name, [RoleNames.Teacher], Array.Empty<Guid>(), true));
        return teacher;
    }

    // ---- Profile create / update ----

    [Fact]
    public async Task Create_ForAUserWithTheTeacherRole_CreatesAProfileWithNoBranchesYet()
    {
        var userId = Guid.NewGuid();
        _identity.AddUser(new AuthenticatedUser(userId, "ahmed@codecamp.demo", "Ahmed Nabil", [RoleNames.Teacher], Array.Empty<Guid>(), true));

        var dto = await new CreateTeacherCommandHandler(Context, _identity)
            .Handle(new CreateTeacherCommand(userId, new DateOnly(2024, 1, 1), PayType.Hourly, 150), CancellationToken.None);

        Assert.Equal("Ahmed Nabil", dto.FullName);
        Assert.Empty(dto.BranchIds);
        Assert.Single(Context.Teachers);
    }

    [Fact]
    public async Task Create_ForAnUnknownUser_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new CreateTeacherCommandHandler(Context, _identity)
                .Handle(new CreateTeacherCommand(Guid.NewGuid(), new DateOnly(2024, 1, 1), PayType.Hourly, 150), CancellationToken.None));
    }

    [Fact]
    public async Task Create_ForAUserWithoutTheTeacherRole_ThrowsBadRequest()
    {
        var userId = Guid.NewGuid();
        _identity.AddUser(new AuthenticatedUser(userId, "fd@codecamp.demo", "Front Desk", [RoleNames.FrontDesk], Array.Empty<Guid>(), true));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            new CreateTeacherCommandHandler(Context, _identity)
                .Handle(new CreateTeacherCommand(userId, new DateOnly(2024, 1, 1), PayType.Hourly, 150), CancellationToken.None));

        Assert.Empty(Context.Teachers);
    }

    [Fact]
    public async Task Create_ASecondProfileForTheSameUser_ThrowsBadRequest()
    {
        var existing = AddTeacherWithAccount("Ahmed", _smouha);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            new CreateTeacherCommandHandler(Context, _identity)
                .Handle(new CreateTeacherCommand(existing.UserId, new DateOnly(2024, 1, 1), PayType.Fixed, 5000), CancellationToken.None));

        Assert.Single(Context.Teachers);
    }

    [Theory]
    [InlineData(PayType.Percentage, 100, true)]
    [InlineData(PayType.Percentage, 100.01, false)]
    [InlineData(PayType.Hourly, 150, true)]
    [InlineData(PayType.Fixed, 20000, true)]
    [InlineData(PayType.PerSession, 0, false)]
    public void CreateValidator_PayRateRules(PayType type, decimal rate, bool valid)
    {
        var result = new CreateTeacherCommandValidator().Validate(new CreateTeacherCommand(Guid.NewGuid(), new DateOnly(2024, 1, 1), type, rate));

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public async Task Update_ChangesPayTermsAndKeepsBranches()
    {
        var teacher = AddTeacherWithAccount("Ahmed", _smouha);

        var dto = await new UpdateTeacherCommandHandler(Context, _identity)
            .Handle(new UpdateTeacherCommand(teacher.Id, new DateOnly(2023, 5, 5), PayType.Percentage, 40), CancellationToken.None);

        Assert.Equal(PayType.Percentage, dto.PayType);
        Assert.Equal(40, dto.PayRate);
        Assert.Equal([_smouha.Id], dto.BranchIds);
    }

    [Fact]
    public async Task Update_UnknownTeacher_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateTeacherCommandHandler(Context, _identity)
                .Handle(new UpdateTeacherCommand(Guid.NewGuid(), new DateOnly(2023, 5, 5), PayType.Hourly, 10), CancellationToken.None));
    }

    // ---- Branch assignment ----

    [Fact]
    public async Task AddToBranch_AssignsTheTeacher_AndRejectsADuplicate()
    {
        var teacher = AddTeacher();
        ActAs(RoleNames.BranchManager, _smouha);
        var handler = new AddTeacherToBranchCommandHandler(Context, CurrentUser);

        await handler.Handle(new AddTeacherToBranchCommand(teacher.Id, _smouha.Id), CancellationToken.None);

        Assert.Single(Context.TeacherBranches);
        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(new AddTeacherToBranchCommand(teacher.Id, _smouha.Id), CancellationToken.None));
    }

    [Fact]
    public async Task AddToBranch_ABranchManagerCannotAssignToAnotherBranch()
    {
        var teacher = AddTeacher();
        ActAs(RoleNames.BranchManager, _smouha);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new AddTeacherToBranchCommandHandler(Context, CurrentUser).Handle(new AddTeacherToBranchCommand(teacher.Id, _kafrAbdo.Id), CancellationToken.None));

        Assert.Empty(Context.TeacherBranches);
    }

    [Fact]
    public async Task AddToBranch_UnknownTeacherOrBranch_ThrowsNotFound()
    {
        var teacher = AddTeacher();
        ActAs(RoleNames.Owner);
        var handler = new AddTeacherToBranchCommandHandler(Context, CurrentUser);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new AddTeacherToBranchCommand(Guid.NewGuid(), _smouha.Id), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new AddTeacherToBranchCommand(teacher.Id, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task RemoveFromBranch_RemovesOnlyThatAssignment_AndOnlyForTheOwnBranch()
    {
        var teacher = AddTeacher(_smouha);
        Context.TeacherBranches.Add(new TeacherBranch { TeacherId = teacher.Id, BranchId = _kafrAbdo.Id });
        Context.SaveChanges();

        ActAs(RoleNames.BranchManager, _kafrAbdo);
        var handler = new RemoveTeacherFromBranchCommandHandler(Context, CurrentUser);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(new RemoveTeacherFromBranchCommand(teacher.Id, _smouha.Id), CancellationToken.None));

        await handler.Handle(new RemoveTeacherFromBranchCommand(teacher.Id, _kafrAbdo.Id), CancellationToken.None);

        Assert.Equal(_smouha.Id, Assert.Single(Context.TeacherBranches).BranchId);
    }

    [Fact]
    public async Task RemoveFromBranch_WhenNotAssigned_ThrowsNotFound()
    {
        var teacher = AddTeacher();
        ActAs(RoleNames.Owner);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new RemoveTeacherFromBranchCommandHandler(Context, CurrentUser).Handle(new RemoveTeacherFromBranchCommand(teacher.Id, _smouha.Id), CancellationToken.None));
    }

    // ---- Availability ----

    private static AddAvailabilityCommand Window(Guid teacherId, Guid branchId) =>
        new(teacherId, branchId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0));

    [Fact]
    public async Task AddAvailability_ByTheTeacherThemselves_IsAllowed_WithoutAnyBranchRole()
    {
        var teacher = AddTeacher(_smouha);
        CurrentUser.UserId = teacher.UserId;
        CurrentUser.Roles = [RoleNames.Teacher];
        CurrentUser.BranchIds = [];

        var dto = await new AddAvailabilityCommandHandler(Context, CurrentUser).Handle(Window(teacher.Id, _smouha.Id), CancellationToken.None);

        Assert.Equal(DayOfWeek.Monday, dto.DayOfWeek);
        Assert.Single(Context.TeacherAvailabilities);
    }

    [Fact]
    public async Task AddAvailability_ForABranchTheTeacherIsNotAssignedTo_ThrowsBadRequest()
    {
        var teacher = AddTeacher(_smouha);
        ActAs(RoleNames.Owner);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            new AddAvailabilityCommandHandler(Context, CurrentUser).Handle(Window(teacher.Id, _kafrAbdo.Id), CancellationToken.None));
    }

    [Fact]
    public async Task AddAvailability_ByAnotherTeacher_ThrowsForbidden()
    {
        var teacher = AddTeacher(_smouha);
        var stranger = AddTeacher(_smouha);
        CurrentUser.UserId = stranger.UserId;
        CurrentUser.Roles = [RoleNames.Teacher];
        CurrentUser.BranchIds = [];

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new AddAvailabilityCommandHandler(Context, CurrentUser).Handle(Window(teacher.Id, _smouha.Id), CancellationToken.None));
    }

    [Fact]
    public void AddAvailabilityValidator_RequiresTheWindowToEndAfterItStarts()
    {
        var validator = new AddAvailabilityCommandValidator();

        Assert.True(validator.Validate(Window(Guid.NewGuid(), Guid.NewGuid())).IsValid);
        Assert.False(validator.Validate(Window(Guid.NewGuid(), Guid.NewGuid()) with { EndTime = new TimeOnly(9, 0) }).IsValid);
    }

    [Fact]
    public async Task RemoveAvailability_OwnRecordAllowed_OtherBranchForbidden()
    {
        var teacher = AddTeacher(_smouha);
        var window = new TeacherAvailability
        {
            Id = Guid.NewGuid(), TeacherId = teacher.Id, BranchId = _smouha.Id,
            DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(12, 0)
        };
        Context.TeacherAvailabilities.Add(window);
        Context.SaveChanges();

        ActAs(RoleNames.BranchManager, _kafrAbdo);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new RemoveAvailabilityCommandHandler(Context, CurrentUser).Handle(new RemoveAvailabilityCommand(window.Id), CancellationToken.None));

        CurrentUser.UserId = teacher.UserId;
        CurrentUser.Roles = [RoleNames.Teacher];
        CurrentUser.BranchIds = [];
        await new RemoveAvailabilityCommandHandler(Context, CurrentUser).Handle(new RemoveAvailabilityCommand(window.Id), CancellationToken.None);

        Assert.Empty(Context.TeacherAvailabilities);
    }

    [Fact]
    public async Task GetAvailability_ReturnsWindowsOrderedByDayThenStart_ForAuthorizedUsersOnly()
    {
        var teacher = AddTeacher(_smouha);
        Context.TeacherAvailabilities.AddRange(
            new TeacherAvailability { Id = Guid.NewGuid(), TeacherId = teacher.Id, BranchId = _smouha.Id, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0) },
            new TeacherAvailability { Id = Guid.NewGuid(), TeacherId = teacher.Id, BranchId = _smouha.Id, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(14, 0), EndTime = new TimeOnly(15, 0) },
            new TeacherAvailability { Id = Guid.NewGuid(), TeacherId = teacher.Id, BranchId = _smouha.Id, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0) });
        Context.SaveChanges();

        ActAs(RoleNames.BranchManager, _smouha);
        var result = await new GetAvailabilityForTeacherQueryHandler(Context, CurrentUser)
            .Handle(new GetAvailabilityForTeacherQuery(teacher.Id), CancellationToken.None);

        Assert.Equal([(DayOfWeek.Monday, new TimeOnly(9, 0)), (DayOfWeek.Monday, new TimeOnly(14, 0)), (DayOfWeek.Wednesday, new TimeOnly(9, 0))],
            result.Select(r => (r.DayOfWeek, r.StartTime)).ToArray());

        ActAs(RoleNames.BranchManager, _kafrAbdo);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetAvailabilityForTeacherQueryHandler(Context, CurrentUser).Handle(new GetAvailabilityForTeacherQuery(teacher.Id), CancellationToken.None));
    }

    // ---- Declared qualifications ----

    [Fact]
    public async Task AddQualification_RecordsIt_AndRejectsADuplicate()
    {
        var teacher = AddTeacher(_smouha);
        var course = AddCourse(_smouha);
        ActAs(RoleNames.BranchManager, _smouha);
        var handler = new AddTeacherQualificationCommandHandler(Context, CurrentUser);

        await handler.Handle(new AddTeacherQualificationCommand(teacher.Id, course.Id), CancellationToken.None);

        Assert.Single(Context.TeacherCourseQualifications);
        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(new AddTeacherQualificationCommand(teacher.Id, course.Id), CancellationToken.None));
    }

    [Fact]
    public async Task AddQualification_ForACourseAtAnotherBranch_ThrowsForbidden()
    {
        var teacher = AddTeacher(_smouha);
        var course = AddCourse(_kafrAbdo);
        ActAs(RoleNames.BranchManager, _smouha);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new AddTeacherQualificationCommandHandler(Context, CurrentUser).Handle(new AddTeacherQualificationCommand(teacher.Id, course.Id), CancellationToken.None));
    }

    [Fact]
    public async Task AddQualification_UnknownTeacherOrCourse_ThrowsNotFound()
    {
        var teacher = AddTeacher(_smouha);
        ActAs(RoleNames.Owner);
        var handler = new AddTeacherQualificationCommandHandler(Context, CurrentUser);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new AddTeacherQualificationCommand(Guid.NewGuid(), AddCourse(_smouha).Id), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new AddTeacherQualificationCommand(teacher.Id, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task RemoveQualification_RemovesIt_ButNotAcrossBranches()
    {
        var teacher = AddTeacher(_smouha);
        var course = AddCourse(_smouha);
        Context.TeacherCourseQualifications.Add(new TeacherCourseQualification { TeacherId = teacher.Id, CourseId = course.Id });
        Context.SaveChanges();

        ActAs(RoleNames.BranchManager, _kafrAbdo);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new RemoveTeacherQualificationCommandHandler(Context, CurrentUser).Handle(new RemoveTeacherQualificationCommand(teacher.Id, course.Id), CancellationToken.None));

        ActAs(RoleNames.BranchManager, _smouha);
        await new RemoveTeacherQualificationCommandHandler(Context, CurrentUser).Handle(new RemoveTeacherQualificationCommand(teacher.Id, course.Id), CancellationToken.None);
        Assert.Empty(Context.TeacherCourseQualifications);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new RemoveTeacherQualificationCommandHandler(Context, CurrentUser).Handle(new RemoveTeacherQualificationCommand(teacher.Id, course.Id), CancellationToken.None));
    }

    [Fact]
    public async Task GetQualifications_ListsCourseNamesAlphabetically_ForAuthorizedUsersOnly()
    {
        var teacher = AddTeacher(_smouha);
        Context.TeacherCourseQualifications.AddRange(
            new TeacherCourseQualification { TeacherId = teacher.Id, CourseId = AddCourse(_smouha, name: "Web Dev").Id },
            new TeacherCourseQualification { TeacherId = teacher.Id, CourseId = AddCourse(_smouha, name: "Python").Id });
        Context.SaveChanges();

        ActAs(RoleNames.BranchManager, _smouha);
        var result = await new GetQualificationsForTeacherQueryHandler(Context, CurrentUser)
            .Handle(new GetQualificationsForTeacherQuery(teacher.Id), CancellationToken.None);
        Assert.Equal(["Python", "Web Dev"], result.Select(q => q.CourseName).ToArray());

        ActAs(RoleNames.BranchManager, _kafrAbdo);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetQualificationsForTeacherQueryHandler(Context, CurrentUser).Handle(new GetQualificationsForTeacherQuery(teacher.Id), CancellationToken.None));
    }

    // ---- Reads ----

    [Fact]
    public async Task GetTeachers_BranchManagersSeeOnlyTeachersAssignedToTheirBranch_OwnerSeesAll()
    {
        AddTeacherWithAccount("Ahmed", _smouha);
        AddTeacherWithAccount("Sara", _kafrAbdo);
        AddTeacherWithAccount("Floaty", _smouha, _kafrAbdo);

        ActAs(RoleNames.BranchManager, _smouha);
        var managerView = await new GetTeachersQueryHandler(Context, CurrentUser, _identity).Handle(new GetTeachersQuery(), CancellationToken.None);

        ActAs(RoleNames.Owner);
        var ownerView = await new GetTeachersQueryHandler(Context, CurrentUser, _identity).Handle(new GetTeachersQuery(), CancellationToken.None);

        Assert.Equal(["Ahmed", "Floaty"], managerView.Select(t => t.FullName).OrderBy(n => n).ToArray());
        Assert.Equal(3, ownerView.Count);
    }

    [Fact]
    public async Task GetTeacherById_OwnRecordAndBranchManagersAllowed_OthersForbidden()
    {
        var teacher = AddTeacherWithAccount("Ahmed", _smouha);
        var handler = () => new GetTeacherByIdQueryHandler(Context, CurrentUser, _identity);

        CurrentUser.UserId = teacher.UserId;
        CurrentUser.Roles = [RoleNames.Teacher];
        CurrentUser.BranchIds = [];
        Assert.Equal("Ahmed", (await handler().Handle(new GetTeacherByIdQuery(teacher.Id), CancellationToken.None)).FullName);

        ActAs(RoleNames.BranchManager, _smouha);
        Assert.Equal("Ahmed", (await handler().Handle(new GetTeacherByIdQuery(teacher.Id), CancellationToken.None)).FullName);

        ActAs(RoleNames.BranchManager, _kafrAbdo);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler().Handle(new GetTeacherByIdQuery(teacher.Id), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => handler().Handle(new GetTeacherByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task GetMyProfile_ReturnsTheCallersOwnProfile_OrNotFoundIfTheyHaveNone()
    {
        var teacher = AddTeacherWithAccount("Ahmed", _smouha);

        CurrentUser.UserId = teacher.UserId;
        var mine = await new GetMyTeacherProfileQueryHandler(Context, CurrentUser, _identity).Handle(new GetMyTeacherProfileQuery(), CancellationToken.None);
        Assert.Equal(teacher.Id, mine.Id);

        CurrentUser.UserId = Guid.NewGuid();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetMyTeacherProfileQueryHandler(Context, CurrentUser, _identity).Handle(new GetMyTeacherProfileQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task GetCandidates_ListsTeacherRoleUsersWhoDoNotHaveAProfileYet()
    {
        AddTeacherWithAccount("Profiled", _smouha);
        var pending = new AuthenticatedUser(Guid.NewGuid(), "pending@codecamp.demo", "Pending Teacher", [RoleNames.Teacher], Array.Empty<Guid>(), true);
        _identity.AddUser(pending);
        _identity.AddUser(new AuthenticatedUser(Guid.NewGuid(), "fd@codecamp.demo", "Not A Teacher", [RoleNames.FrontDesk], Array.Empty<Guid>(), true));

        var result = await new GetTeacherCandidatesQueryHandler(Context, _identity).Handle(new GetTeacherCandidatesQuery(), CancellationToken.None);

        Assert.Equal(pending.UserId, Assert.Single(result).UserId);
    }

    [Fact]
    public async Task GetMySchedule_ReturnsOnlyTheCallersSessions_InTimeOrder()
    {
        var mine = AddTeacher(_smouha);
        var other = AddTeacher(_smouha);
        var course = AddCourse(_smouha);
        var room = AddRoom(_smouha);
        var later = AddSession(course, room, mine, new DateTime(2030, 1, 8, 10, 0, 0, DateTimeKind.Utc));
        var earlier = AddSession(course, room, mine, new DateTime(2030, 1, 7, 10, 0, 0, DateTimeKind.Utc));
        AddSession(course, room, other, new DateTime(2030, 1, 7, 12, 0, 0, DateTimeKind.Utc));
        CurrentUser.UserId = mine.UserId;

        var result = await new GetMyScheduleQueryHandler(Context, CurrentUser).Handle(new GetMyScheduleQuery(), CancellationToken.None);

        Assert.Equal([earlier.Id, later.Id], result.Select(s => s.Id).ToArray());
    }
}

using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Application.Tests.TestSupport;
using CEMS.Application.Users.Commands.CreateStaffUser;
using CEMS.Application.Users.Commands.SetStaffUserActive;
using CEMS.Application.Users.Queries.GetBranchStaff;
using CEMS.Application.Users.Queries.GetStaffUsers;
using CEMS.Domain.Users;

namespace CEMS.Application.Tests.Users;

public class StaffHandlerTests : SeededHandlerTestBase
{
    private readonly TestIdentityService _identity = new();

    private CreateStaffUserCommand NewStaff(string role, Guid? branchId, string email = "new@codecamp.demo") =>
        new(email, "DemoPass123", "New Person", "01000000000", role, branchId);

    private Task<CEMS.Application.Users.StaffUserDto> Create(CreateStaffUserCommand command) =>
        new CreateStaffUserCommandHandler(Context, _identity, CurrentUser).Handle(command, CancellationToken.None);

    // ---- CreateStaffUser ----

    [Fact]
    public async Task CreateStaff_OwnerCreatesBranchManager_AssignsTheBranch()
    {
        var branch = AddBranch();
        ActAs(RoleNames.Owner);

        await Create(NewStaff(RoleNames.BranchManager, branch.Id));

        Assert.Equal(branch.Id, Assert.Single(Context.UserBranchAssignments).BranchId);
        Assert.Contains((("new@codecamp.demo"), RoleNames.BranchManager), _identity.CreatedUsers);
    }

    [Fact]
    public async Task CreateStaff_BranchManagerCannotCreateAnotherBranchManager()
    {
        var branch = AddBranch();
        ActAs(RoleNames.BranchManager, branch);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Create(NewStaff(RoleNames.BranchManager, branch.Id)));

        Assert.Empty(_identity.CreatedUsers);
    }

    [Fact]
    public async Task CreateStaff_BranchManagerCreatesFrontDeskAtOwnBranch()
    {
        var branch = AddBranch();
        ActAs(RoleNames.BranchManager, branch);

        await Create(NewStaff(RoleNames.FrontDesk, branch.Id));

        Assert.Single(_identity.CreatedUsers);
        Assert.Single(Context.UserBranchAssignments);
    }

    [Fact]
    public async Task CreateStaff_BranchManagerCannotCreateStaffAtAnotherBranch()
    {
        var mine = AddBranch("Smouha");
        var other = AddBranch("Kafr Abdo");
        ActAs(RoleNames.BranchManager, mine);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Create(NewStaff(RoleNames.FrontDesk, other.Id)));

        Assert.Empty(_identity.CreatedUsers);
        Assert.Empty(Context.UserBranchAssignments);
    }

    [Fact]
    public async Task CreateStaff_UnknownBranch_ThrowsNotFound()
    {
        ActAs(RoleNames.Owner);

        await Assert.ThrowsAsync<NotFoundException>(() => Create(NewStaff(RoleNames.FrontDesk, Guid.NewGuid())));
    }

    [Fact]
    public async Task CreateStaff_TeacherHasNoBranchAssignment()
    {
        // A teacher's branches come from their Teacher profile, not from UserBranchAssignments.
        ActAs(RoleNames.Owner);

        await Create(NewStaff(RoleNames.Teacher, null));

        Assert.Empty(Context.UserBranchAssignments);
    }

    [Fact]
    public async Task CreateStaff_WhenIdentityRejectsIt_ThrowsBadRequestAndAssignsNoBranch()
    {
        var branch = AddBranch();
        ActAs(RoleNames.Owner);
        _identity.CreateUserErrors = ["Email is already taken."];

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => Create(NewStaff(RoleNames.FrontDesk, branch.Id)));

        Assert.Equal(["Email is already taken."], ex.Errors);
        Assert.Empty(Context.UserBranchAssignments);
    }

    [Theory]
    [InlineData("Owner")]
    [InlineData("Parent")]
    [InlineData("")]
    public void CreateStaffValidator_RejectsRolesThatCannotBeAssigned(string role)
    {
        var result = new CreateStaffUserCommandValidator().Validate(NewStaff(role, Guid.NewGuid()));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateStaffUserCommand.Role));
    }

    [Theory]
    [InlineData("BranchManager")]
    [InlineData("FrontDesk")]
    public void CreateStaffValidator_RequiresABranchForBranchBoundRoles(string role)
    {
        var result = new CreateStaffUserCommandValidator().Validate(NewStaff(role, null));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateStaffUserCommand.BranchId));
    }

    [Fact]
    public void CreateStaffValidator_AllowsATeacherWithoutABranch_ButRequiresAStrongEnoughPassword()
    {
        var validator = new CreateStaffUserCommandValidator();

        Assert.True(validator.Validate(NewStaff(RoleNames.Teacher, null)).IsValid);
        Assert.False(validator.Validate(NewStaff(RoleNames.Teacher, null) with { Password = "short" }).IsValid);
    }

    // ---- GetBranchStaff ----

    private static AuthenticatedUser Staff(string name, string role, params Guid[] branchIds) =>
        new(Guid.NewGuid(), $"{name.ToLower()}@codecamp.demo", name, [role], branchIds, true);

    [Fact]
    public async Task GetBranchStaff_ReturnsOnlyTeachersAndFrontDeskAtTheManagersBranch_SortedByName()
    {
        var smouha = AddBranch("Smouha");
        var kafrAbdo = AddBranch("Kafr Abdo");
        ActAs(RoleNames.BranchManager, smouha);

        var frontDesk = Staff("Mariam", RoleNames.FrontDesk, smouha.Id);
        var otherFrontDesk = Staff("Other", RoleNames.FrontDesk, kafrAbdo.Id);
        var otherManager = Staff("Peer", RoleNames.BranchManager, smouha.Id);
        var owner = Staff("Boss", RoleNames.Owner);
        var teacher = Staff("Ahmed", RoleNames.Teacher);
        var teacherElsewhere = Staff("Sara", RoleNames.Teacher);
        foreach (var user in new[] { frontDesk, otherFrontDesk, otherManager, owner, teacher, teacherElsewhere })
        {
            _identity.AddUser(user);
        }

        Context.Teachers.AddRange(
            new CEMS.Domain.Teachers.Teacher { Id = Guid.NewGuid(), UserId = teacher.UserId, HireDate = new DateOnly(2024, 1, 1), PayRate = 100 },
            new CEMS.Domain.Teachers.Teacher { Id = Guid.NewGuid(), UserId = teacherElsewhere.UserId, HireDate = new DateOnly(2024, 1, 1), PayRate = 100 });
        Context.SaveChanges();
        Context.TeacherBranches.Add(new CEMS.Domain.Teachers.TeacherBranch { TeacherId = Context.Teachers.Single(t => t.UserId == teacher.UserId).Id, BranchId = smouha.Id });
        Context.TeacherBranches.Add(new CEMS.Domain.Teachers.TeacherBranch { TeacherId = Context.Teachers.Single(t => t.UserId == teacherElsewhere.UserId).Id, BranchId = kafrAbdo.Id });
        Context.SaveChanges();

        var result = await new GetBranchStaffQueryHandler(Context, _identity, CurrentUser)
            .Handle(new GetBranchStaffQuery(), CancellationToken.None);

        Assert.Equal(["Ahmed", "Mariam"], result.Select(r => r.FullName).ToArray());
    }

    [Fact]
    public async Task GetBranchStaff_ForAFloatingTeacher_OnlyExposesTheManagersOwnBranch()
    {
        var smouha = AddBranch("Smouha");
        var kafrAbdo = AddBranch("Kafr Abdo");
        ActAs(RoleNames.BranchManager, smouha);

        var floating = Staff("Floaty", RoleNames.Teacher);
        _identity.AddUser(floating);
        var teacher = AddTeacher();
        teacher.UserId = floating.UserId;
        Context.TeacherBranches.AddRange(
            new CEMS.Domain.Teachers.TeacherBranch { TeacherId = teacher.Id, BranchId = smouha.Id },
            new CEMS.Domain.Teachers.TeacherBranch { TeacherId = teacher.Id, BranchId = kafrAbdo.Id });
        Context.SaveChanges();

        var result = await new GetBranchStaffQueryHandler(Context, _identity, CurrentUser)
            .Handle(new GetBranchStaffQuery(), CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal([smouha.Id], dto.BranchIds);
    }

    // ---- GetStaffUsers / SetStaffUserActive ----

    [Fact]
    public async Task GetStaffUsers_ReturnsEveryStaffAccountWithItsRolesAndBranches()
    {
        var branch = AddBranch();
        _identity.AddUser(Staff("Mariam", RoleNames.FrontDesk, branch.Id));
        _identity.AddUser(Staff("Boss", RoleNames.Owner));

        var result = await new GetStaffUsersQueryHandler(_identity).Handle(new GetStaffUsersQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.FullName == "Mariam" && r.BranchIds.Contains(branch.Id));
    }

    [Fact]
    public async Task SetStaffUserActive_DelegatesToTheIdentityService()
    {
        var userId = Guid.NewGuid();

        await new SetStaffUserActiveCommandHandler(_identity).Handle(new SetStaffUserActiveCommand(userId, false), CancellationToken.None);

        Assert.Equal((userId, false), Assert.Single(_identity.SetActiveCalls));
    }
}

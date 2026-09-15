using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Application.Tests.TestSupport;
using CEMS.Application.Users.Commands.ResetStaffPassword;
using CEMS.Domain.Branches;
using CEMS.Domain.Teachers;
using CEMS.Domain.Users;

namespace CEMS.Application.Tests.Users;

public class ResetStaffPasswordCommandHandlerTests : HandlerTestBase
{
    private readonly TestIdentityService _identityService = new();
    private readonly Guid _downtownId = Guid.NewGuid();
    private readonly Guid _uptownId = Guid.NewGuid();

    public ResetStaffPasswordCommandHandlerTests()
    {
        // Real Branch rows, since TeacherBranch has a genuine FK to Branches -- AuthenticatedUser and
        // ICurrentUserService's own BranchIds lists have no such constraint, but TeacherBranch does.
        Context.Branches.Add(new Branch { Id = _downtownId, Name = "Downtown" });
        Context.Branches.Add(new Branch { Id = _uptownId, Name = "Uptown" });
        Context.SaveChanges();
    }

    private ResetStaffPasswordCommandHandler CreateHandler() => new(_identityService, CurrentUser, Context);

    [Fact]
    public async Task Handle_Owner_CanResetAnyone()
    {
        var targetId = Guid.NewGuid();
        _identityService.AddUser(new AuthenticatedUser(targetId, "fd@cems.demo", "Frankie Desk", [RoleNames.FrontDesk], [_downtownId], true));
        CurrentUser.UserId = Guid.NewGuid();
        CurrentUser.Roles = [RoleNames.Owner];

        var handler = CreateHandler();
        await handler.Handle(new ResetStaffPasswordCommand(targetId, "NewPass123"), CancellationToken.None);

        Assert.Contains(_identityService.ResetPasswordCalls, c => c.UserId == targetId && c.NewPassword == "NewPass123");
    }

    [Fact]
    public async Task Handle_BranchManager_CanResetSameBranchFrontDesk()
    {
        var targetId = Guid.NewGuid();
        _identityService.AddUser(new AuthenticatedUser(targetId, "fd@cems.demo", "Frankie Desk", [RoleNames.FrontDesk], [_downtownId], true));
        CurrentUser.UserId = Guid.NewGuid();
        CurrentUser.Roles = [RoleNames.BranchManager];
        CurrentUser.BranchIds = [_downtownId];

        var handler = CreateHandler();
        await handler.Handle(new ResetStaffPasswordCommand(targetId, "NewPass123"), CancellationToken.None);

        Assert.Contains(_identityService.ResetPasswordCalls, c => c.UserId == targetId);
    }

    [Fact]
    public async Task Handle_BranchManager_CanResetSameBranchTeacher_ViaTeacherBranchNotUserBranchAssignments()
    {
        // Regression test for the bug caught during manual verification: a Teacher's branch access
        // lives on TeacherBranch (their Teacher profile), never on UserBranchAssignments -- so the
        // AuthenticatedUser.BranchIds returned by identity is always empty for a Teacher. The handler
        // must fall back to TeacherBranch for the branch check when the target is a Teacher.
        var targetUserId = Guid.NewGuid();
        var teacher = new Teacher { Id = Guid.NewGuid(), UserId = targetUserId, HireDate = DateOnly.FromDateTime(DateTime.UtcNow), PayType = PayType.Hourly, PayRate = 100 };
        Context.Teachers.Add(teacher);
        Context.TeacherBranches.Add(new TeacherBranch { TeacherId = teacher.Id, BranchId = _downtownId });
        Context.SaveChanges();

        _identityService.AddUser(new AuthenticatedUser(targetUserId, "teacher@cems.demo", "Tara Teacher", [RoleNames.Teacher], [], true)); // BranchIds empty, as it always is for a Teacher

        CurrentUser.UserId = Guid.NewGuid();
        CurrentUser.Roles = [RoleNames.BranchManager];
        CurrentUser.BranchIds = [_downtownId];

        var handler = CreateHandler();
        await handler.Handle(new ResetStaffPasswordCommand(targetUserId, "NewPass123"), CancellationToken.None);

        Assert.Contains(_identityService.ResetPasswordCalls, c => c.UserId == targetUserId);
    }

    [Fact]
    public async Task Handle_BranchManager_CannotResetDifferentBranchTeacher()
    {
        var targetUserId = Guid.NewGuid();
        var teacher = new Teacher { Id = Guid.NewGuid(), UserId = targetUserId, HireDate = DateOnly.FromDateTime(DateTime.UtcNow), PayType = PayType.Hourly, PayRate = 100 };
        Context.Teachers.Add(teacher);
        Context.TeacherBranches.Add(new TeacherBranch { TeacherId = teacher.Id, BranchId = _uptownId });
        Context.SaveChanges();

        _identityService.AddUser(new AuthenticatedUser(targetUserId, "teacher@cems.demo", "Tom Teacher", [RoleNames.Teacher], [], true));

        CurrentUser.UserId = Guid.NewGuid();
        CurrentUser.Roles = [RoleNames.BranchManager];
        CurrentUser.BranchIds = [_downtownId]; // manages Downtown, teacher is at Uptown

        var handler = CreateHandler();

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => handler.Handle(new ResetStaffPasswordCommand(targetUserId, "NewPass123"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_BranchManager_CannotResetAnotherBranchManager()
    {
        var targetId = Guid.NewGuid();
        _identityService.AddUser(new AuthenticatedUser(targetId, "bm2@cems.demo", "Other Manager", [RoleNames.BranchManager], [_downtownId], true));

        CurrentUser.UserId = Guid.NewGuid();
        CurrentUser.Roles = [RoleNames.BranchManager];
        CurrentUser.BranchIds = [_downtownId];

        var handler = CreateHandler();

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => handler.Handle(new ResetStaffPasswordCommand(targetId, "NewPass123"), CancellationToken.None));
    }
}

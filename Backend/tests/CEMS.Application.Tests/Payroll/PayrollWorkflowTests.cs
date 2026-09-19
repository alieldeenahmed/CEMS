using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Application.Payroll;
using CEMS.Application.Payroll.Commands.ApprovePayrollRun;
using CEMS.Application.Payroll.Commands.ApproveStaffPayrollRun;
using CEMS.Application.Payroll.Commands.GenerateStaffPayrollRun;
using CEMS.Application.Payroll.Commands.MarkPayrollRunPaid;
using CEMS.Application.Payroll.Commands.MarkStaffPayrollRunPaid;
using CEMS.Application.Payroll.Commands.UpdateStaffPayrollRun;
using CEMS.Application.Payroll.Queries.GeneratePayStub;
using CEMS.Application.Payroll.Queries.GenerateStaffPayStub;
using CEMS.Application.Payroll.Queries.GetLineItemsForPayrollRun;
using CEMS.Application.Payroll.Queries.GetMyPayrollRuns;
using CEMS.Application.Payroll.Queries.GetMyStaffPayrollRuns;
using CEMS.Application.Payroll.Queries.GetPayrollRunById;
using CEMS.Application.Payroll.Queries.GetPayrollRunsForTeacher;
using CEMS.Application.Payroll.Queries.GetStaffPayrollRunsForUser;
using CEMS.Application.Tests.TestSupport;
using CEMS.Domain.Payroll;
using CEMS.Domain.Users;

namespace CEMS.Application.Tests.Payroll;

/// <summary>
/// The Draft → Approved → Paid lifecycle (teacher and staff runs), staff payroll rules, and who may see
/// a payroll run or its pay stub.
/// </summary>
public class PayrollWorkflowTests : SeededHandlerTestBase
{
    private class CapturingPayStubGenerator : IPayStubGenerator
    {
        public PayStubData? Captured { get; private set; }

        public byte[] Generate(PayStubData data)
        {
            Captured = data;
            return [9, 9];
        }
    }

    private static readonly DateOnly Start = new(2030, 1, 1);
    private static readonly DateOnly End = new(2030, 1, 31);

    private readonly TestIdentityService _identity = new();

    private PayrollRun AddRun(Domain.Teachers.Teacher teacher, PayrollRunStatus status = PayrollRunStatus.Draft, decimal total = 500, DateOnly? start = null)
    {
        var run = new PayrollRun
        {
            Id = Guid.NewGuid(), TeacherId = teacher.Id, PeriodStart = start ?? Start,
            PeriodEnd = (start ?? Start).AddDays(30), TotalAmount = total, Status = status
        };
        Context.PayrollRuns.Add(run);
        Context.SaveChanges();
        return run;
    }

    private StaffPayrollRun AddStaffRun(Guid userId, PayrollRunStatus status = PayrollRunStatus.Draft, decimal amount = 3000, DateOnly? start = null)
    {
        var run = new StaffPayrollRun
        {
            Id = Guid.NewGuid(), UserId = userId, PeriodStart = start ?? Start,
            PeriodEnd = (start ?? Start).AddDays(30), Amount = amount, Status = status
        };
        Context.StaffPayrollRuns.Add(run);
        Context.SaveChanges();
        return run;
    }

    private AuthenticatedUser AddStaff(string role, string name = "Mariam Younis")
    {
        var user = new AuthenticatedUser(Guid.NewGuid(), $"{name.Split(' ')[0].ToLower()}@codecamp.demo", name, [role], Array.Empty<Guid>(), true);
        _identity.AddUser(user);
        return user;
    }

    // ---- Teacher run lifecycle ----

    [Fact]
    public async Task TeacherRun_MovesDraftToApprovedToPaid_InThatOrderOnly()
    {
        var run = AddRun(AddTeacher());
        var approve = new ApprovePayrollRunCommandHandler(Context);
        var pay = new MarkPayrollRunPaidCommandHandler(Context);

        await Assert.ThrowsAsync<BadRequestException>(() => pay.Handle(new MarkPayrollRunPaidCommand(run.Id), CancellationToken.None));

        Assert.Equal(PayrollRunStatus.Approved, (await approve.Handle(new ApprovePayrollRunCommand(run.Id), CancellationToken.None)).Status);
        await Assert.ThrowsAsync<BadRequestException>(() => approve.Handle(new ApprovePayrollRunCommand(run.Id), CancellationToken.None));

        Assert.Equal(PayrollRunStatus.Paid, (await pay.Handle(new MarkPayrollRunPaidCommand(run.Id), CancellationToken.None)).Status);
        await Assert.ThrowsAsync<BadRequestException>(() => pay.Handle(new MarkPayrollRunPaidCommand(run.Id), CancellationToken.None));
        await Assert.ThrowsAsync<BadRequestException>(() => approve.Handle(new ApprovePayrollRunCommand(run.Id), CancellationToken.None));
    }

    [Fact]
    public async Task TeacherRun_UnknownRunIsNotFound_OnApproveAndPay()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new ApprovePayrollRunCommandHandler(Context).Handle(new ApprovePayrollRunCommand(Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new MarkPayrollRunPaidCommandHandler(Context).Handle(new MarkPayrollRunPaidCommand(Guid.NewGuid()), CancellationToken.None));
    }

    // ---- Staff runs ----

    [Theory]
    [InlineData("FrontDesk")]
    [InlineData("BranchManager")]
    public async Task StaffRun_CanBeGeneratedForFrontDeskAndBranchManagers(string role)
    {
        var user = AddStaff(role);

        var dto = await new GenerateStaffPayrollRunCommandHandler(Context, _identity)
            .Handle(new GenerateStaffPayrollRunCommand(user.UserId, Start, End, 3500), CancellationToken.None);

        Assert.Equal((PayrollRunStatus.Draft, 3500m), (dto.Status, dto.Amount));
    }

    [Fact]
    public async Task StaffRun_IsRejectedForTeachersAndOwners_WhoHaveAnotherPayrollPath()
    {
        var teacher = AddStaff(RoleNames.Teacher, "Ahmed Nabil");
        var owner = AddStaff(RoleNames.Owner, "Mostafa");
        var handler = new GenerateStaffPayrollRunCommandHandler(Context, _identity);

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(new GenerateStaffPayrollRunCommand(teacher.UserId, Start, End, 100), CancellationToken.None));
        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(new GenerateStaffPayrollRunCommand(owner.UserId, Start, End, 100), CancellationToken.None));
        Assert.Empty(Context.StaffPayrollRuns);
    }

    [Fact]
    public async Task StaffRun_UnknownUser_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GenerateStaffPayrollRunCommandHandler(Context, _identity)
                .Handle(new GenerateStaffPayrollRunCommand(Guid.NewGuid(), Start, End, 100), CancellationToken.None));
    }

    [Fact]
    public async Task StaffRun_OverlappingPeriodsAreRejected_ButAdjacentOnesAreFine()
    {
        var user = AddStaff(RoleNames.FrontDesk);
        var handler = new GenerateStaffPayrollRunCommandHandler(Context, _identity);
        await handler.Handle(new GenerateStaffPayrollRunCommand(user.UserId, Start, End, 3000), CancellationToken.None);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new GenerateStaffPayrollRunCommand(user.UserId, End, End.AddDays(30), 3000), CancellationToken.None));

        await handler.Handle(new GenerateStaffPayrollRunCommand(user.UserId, End.AddDays(1), End.AddDays(30), 3000), CancellationToken.None);
        Assert.Equal(2, Context.StaffPayrollRuns.Count());
    }

    [Fact]
    public async Task StaffRun_AmountCanBeEditedOnlyWhileDraft()
    {
        var run = AddStaffRun(Guid.NewGuid());
        var update = new UpdateStaffPayrollRunCommandHandler(Context);

        var edited = await update.Handle(new UpdateStaffPayrollRunCommand(run.Id, 4200), CancellationToken.None);
        Assert.Equal(4200, edited.Amount);

        await new ApproveStaffPayrollRunCommandHandler(Context).Handle(new ApproveStaffPayrollRunCommand(run.Id), CancellationToken.None);

        await Assert.ThrowsAsync<BadRequestException>(() => update.Handle(new UpdateStaffPayrollRunCommand(run.Id, 1), CancellationToken.None));
        Assert.Equal(4200, Context.StaffPayrollRuns.Single().Amount);
        await Assert.ThrowsAsync<NotFoundException>(() => update.Handle(new UpdateStaffPayrollRunCommand(Guid.NewGuid(), 1), CancellationToken.None));
    }

    [Fact]
    public async Task StaffRun_MovesDraftToApprovedToPaid_InThatOrderOnly()
    {
        var run = AddStaffRun(Guid.NewGuid());
        var approve = new ApproveStaffPayrollRunCommandHandler(Context);
        var pay = new MarkStaffPayrollRunPaidCommandHandler(Context);

        await Assert.ThrowsAsync<BadRequestException>(() => pay.Handle(new MarkStaffPayrollRunPaidCommand(run.Id), CancellationToken.None));
        Assert.Equal(PayrollRunStatus.Approved, (await approve.Handle(new ApproveStaffPayrollRunCommand(run.Id), CancellationToken.None)).Status);
        await Assert.ThrowsAsync<BadRequestException>(() => approve.Handle(new ApproveStaffPayrollRunCommand(run.Id), CancellationToken.None));
        Assert.Equal(PayrollRunStatus.Paid, (await pay.Handle(new MarkStaffPayrollRunPaidCommand(run.Id), CancellationToken.None)).Status);
        await Assert.ThrowsAsync<NotFoundException>(() => approve.Handle(new ApproveStaffPayrollRunCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public void StaffRunValidator_RequiresAPositiveAmountAndAValidPeriod()
    {
        var validator = new GenerateStaffPayrollRunCommandValidator();
        var id = Guid.NewGuid();

        Assert.True(validator.Validate(new GenerateStaffPayrollRunCommand(id, Start, End, 100)).IsValid);
        Assert.True(validator.Validate(new GenerateStaffPayrollRunCommand(id, Start, Start, 100)).IsValid);
        Assert.False(validator.Validate(new GenerateStaffPayrollRunCommand(id, Start, End, 0)).IsValid);
        Assert.False(validator.Validate(new GenerateStaffPayrollRunCommand(id, End, Start, 100)).IsValid);
    }

    // ---- Reads: who sees what ----

    [Fact]
    public async Task GetRunById_OwnerAndTheTeacherThemselvesMaySee_AnotherTeacherMayNot()
    {
        var teacher = AddTeacher();
        var run = AddRun(teacher);

        ActAs(RoleNames.Owner);
        Assert.Equal(run.Id, (await new GetPayrollRunByIdQueryHandler(Context, CurrentUser).Handle(new GetPayrollRunByIdQuery(run.Id), CancellationToken.None)).Id);

        CurrentUser.UserId = teacher.UserId;
        CurrentUser.Roles = [RoleNames.Teacher];
        Assert.Equal(run.Id, (await new GetPayrollRunByIdQueryHandler(Context, CurrentUser).Handle(new GetPayrollRunByIdQuery(run.Id), CancellationToken.None)).Id);

        CurrentUser.UserId = AddTeacher().UserId;
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetPayrollRunByIdQueryHandler(Context, CurrentUser).Handle(new GetPayrollRunByIdQuery(run.Id), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetPayrollRunByIdQueryHandler(Context, CurrentUser).Handle(new GetPayrollRunByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task BranchManagersCannotSeeAPayrollRun_EvenAtTheirOwnBranch()
    {
        var branch = AddBranch();
        var run = AddRun(AddTeacher(branch));
        ActAs(RoleNames.BranchManager, branch);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetPayrollRunByIdQueryHandler(Context, CurrentUser).Handle(new GetPayrollRunByIdQuery(run.Id), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetLineItemsForPayrollRunQueryHandler(Context, CurrentUser).Handle(new GetLineItemsForPayrollRunQuery(run.Id), CancellationToken.None));
    }

    [Fact]
    public async Task LineItems_AreReturnedForTheRunOnly_ToOwnerOrTheTeacher()
    {
        var branch = AddBranch();
        var teacher = AddTeacher(branch);
        var course = AddCourse(branch);
        var session = AddSession(course, AddRoom(branch), teacher, new DateTime(2030, 1, 7, 10, 0, 0, DateTimeKind.Utc));
        var run = AddRun(teacher);
        var otherRun = AddRun(teacher, start: new DateOnly(2031, 1, 1));
        Context.PayrollLineItems.AddRange(
            new PayrollLineItem { Id = Guid.NewGuid(), PayrollRunId = run.Id, CourseSessionId = session.Id, Amount = 100 },
            new PayrollLineItem { Id = Guid.NewGuid(), PayrollRunId = otherRun.Id, CourseSessionId = session.Id, Amount = 999 });
        Context.SaveChanges();
        CurrentUser.UserId = teacher.UserId;
        CurrentUser.Roles = [RoleNames.Teacher];

        var result = await new GetLineItemsForPayrollRunQueryHandler(Context, CurrentUser).Handle(new GetLineItemsForPayrollRunQuery(run.Id), CancellationToken.None);

        Assert.Equal(100, Assert.Single(result).Amount);
    }

    [Fact]
    public async Task MyRuns_ReturnsOnlyTheCallersRuns_NewestPeriodFirst()
    {
        var mine = AddTeacher();
        var other = AddTeacher();
        var older = AddRun(mine, start: new DateOnly(2030, 1, 1));
        var newer = AddRun(mine, start: new DateOnly(2030, 3, 1));
        AddRun(other);
        CurrentUser.UserId = mine.UserId;

        var result = await new GetMyPayrollRunsQueryHandler(Context, CurrentUser).Handle(new GetMyPayrollRunsQuery(), CancellationToken.None);

        Assert.Equal([newer.Id, older.Id], result.Select(r => r.Id).ToArray());
    }

    [Fact]
    public async Task RunsForTeacher_ReturnsThatTeachersRunsNewestFirst()
    {
        var teacher = AddTeacher();
        var older = AddRun(teacher, start: new DateOnly(2030, 1, 1));
        var newer = AddRun(teacher, start: new DateOnly(2030, 3, 1));
        AddRun(AddTeacher());

        var result = await new GetPayrollRunsForTeacherQueryHandler(Context).Handle(new GetPayrollRunsForTeacherQuery(teacher.Id), CancellationToken.None);

        Assert.Equal([newer.Id, older.Id], result.Select(r => r.Id).ToArray());
    }

    [Fact]
    public async Task StaffRunReads_MyRunsAreScopedToTheCaller_AndUserRunsToTheGivenUser()
    {
        var me = Guid.NewGuid();
        var older = AddStaffRun(me, start: new DateOnly(2030, 1, 1));
        var newer = AddStaffRun(me, start: new DateOnly(2030, 3, 1));
        AddStaffRun(Guid.NewGuid());
        CurrentUser.UserId = me;

        var mine = await new GetMyStaffPayrollRunsQueryHandler(Context, CurrentUser).Handle(new GetMyStaffPayrollRunsQuery(), CancellationToken.None);
        var forUser = await new GetStaffPayrollRunsForUserQueryHandler(Context).Handle(new GetStaffPayrollRunsForUserQuery(me), CancellationToken.None);

        Assert.Equal([newer.Id, older.Id], mine.Select(r => r.Id).ToArray());
        Assert.Equal(mine.Select(r => r.Id), forUser.Select(r => r.Id));
    }

    // ---- Pay stubs ----

    [Fact]
    public async Task PayStub_ContainsTheRunsSessionsInTimeOrder_AndIsRenderedByTheGenerator()
    {
        var branch = AddBranch();
        var teacher = AddTeacher(branch);
        _identity.AddUser(new AuthenticatedUser(teacher.UserId, "ahmed@codecamp.demo", "Ahmed Nabil", [RoleNames.Teacher], Array.Empty<Guid>(), true));
        var course = AddCourse(branch, name: "Python Fundamentals");
        var room = AddRoom(branch);
        var second = AddSession(course, room, teacher, new DateTime(2030, 1, 14, 10, 0, 0, DateTimeKind.Utc));
        var first = AddSession(course, room, teacher, new DateTime(2030, 1, 7, 10, 0, 0, DateTimeKind.Utc));
        var run = AddRun(teacher, PayrollRunStatus.Approved, 300);
        Context.PayrollLineItems.AddRange(
            new PayrollLineItem { Id = Guid.NewGuid(), PayrollRunId = run.Id, CourseSessionId = second.Id, Amount = 150 },
            new PayrollLineItem { Id = Guid.NewGuid(), PayrollRunId = run.Id, CourseSessionId = first.Id, Amount = 150 });
        Context.SaveChanges();
        ActAs(RoleNames.Owner);
        var generator = new CapturingPayStubGenerator();

        var bytes = await new GeneratePayStubQueryHandler(Context, CurrentUser, _identity, generator)
            .Handle(new GeneratePayStubQuery(run.Id), CancellationToken.None);

        Assert.Equal([9, 9], bytes);
        Assert.Equal(("Ahmed Nabil", 300m, PayrollRunStatus.Approved), (generator.Captured!.TeacherName, generator.Captured.TotalAmount, generator.Captured.Status));
        Assert.Equal([first.StartUtc, second.StartUtc], generator.Captured.LineItems.Select(l => l.SessionStartUtc).ToArray());
        Assert.All(generator.Captured.LineItems, l => Assert.Equal("Python Fundamentals", l.CourseName));
    }

    [Fact]
    public async Task PayStub_IsRefusedToAnyoneWhoIsNotTheOwnerOrThePayee()
    {
        var teacher = AddTeacher();
        var run = AddRun(teacher);
        ActAs(RoleNames.BranchManager, AddBranch());
        var generator = new CapturingPayStubGenerator();

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GeneratePayStubQueryHandler(Context, CurrentUser, _identity, generator).Handle(new GeneratePayStubQuery(run.Id), CancellationToken.None));

        Assert.Null(generator.Captured);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GeneratePayStubQueryHandler(Context, CurrentUser, _identity, generator).Handle(new GeneratePayStubQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task StaffPayStub_HasNoLineItems_AndIsOnlyForTheOwnerOrThePayee()
    {
        var user = AddStaff(RoleNames.FrontDesk, "Mariam Younis");
        var run = AddStaffRun(user.UserId, PayrollRunStatus.Paid, 3200);
        var generator = new CapturingPayStubGenerator();

        CurrentUser.UserId = user.UserId;
        CurrentUser.Roles = [RoleNames.FrontDesk];
        await new GenerateStaffPayStubQueryHandler(Context, CurrentUser, _identity, generator)
            .Handle(new GenerateStaffPayStubQuery(run.Id), CancellationToken.None);

        Assert.Equal(("Mariam Younis", 3200m), (generator.Captured!.TeacherName, generator.Captured.TotalAmount));
        Assert.Empty(generator.Captured.LineItems);

        CurrentUser.UserId = Guid.NewGuid();
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GenerateStaffPayStubQueryHandler(Context, CurrentUser, _identity, generator).Handle(new GenerateStaffPayStubQuery(run.Id), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GenerateStaffPayStubQueryHandler(Context, CurrentUser, _identity, generator).Handle(new GenerateStaffPayStubQuery(Guid.NewGuid()), CancellationToken.None));
    }
}

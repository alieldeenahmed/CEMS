using CEMS.Application.Common.Exceptions;
using CEMS.Application.Students.Commands.TransferStudentBranch;
using CEMS.Application.Tests.TestSupport;
using CEMS.Domain.Branches;
using CEMS.Domain.Students;
using CEMS.Domain.Users;

namespace CEMS.Application.Tests.Students;

public class TransferStudentBranchCommandHandlerTests : HandlerTestBase
{
    private Guid _studentId;
    private Guid _downtownId;
    private Guid _uptownId;

    private void Seed()
    {
        var downtown = new Branch { Id = Guid.NewGuid(), Name = "Downtown" };
        var uptown = new Branch { Id = Guid.NewGuid(), Name = "Uptown" };
        var student = new Student
        {
            Id = Guid.NewGuid(),
            FullName = "Sam Student",
            DateOfBirth = new DateOnly(2013, 1, 1),
            Gender = Gender.Male,
            EnrollmentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CurrentBranchId = downtown.Id
        };

        Context.AddRange(downtown, uptown, student);
        Context.SaveChanges();

        _studentId = student.Id;
        _downtownId = downtown.Id;
        _uptownId = uptown.Id;

        CurrentUser.UserId = Guid.NewGuid();
        CurrentUser.Roles = [RoleNames.Owner];
    }

    private TransferStudentBranchCommandHandler CreateHandler() => new(Context, CurrentUser);

    [Fact]
    public async Task Handle_ValidTransfer_UpdatesBranchAndRecordsHistory()
    {
        Seed();
        var handler = CreateHandler();

        var result = await handler.Handle(new TransferStudentBranchCommand(_studentId, _uptownId, "Family relocated"), CancellationToken.None);

        Assert.Equal(_uptownId, result.CurrentBranchId);

        var history = Context.StudentBranchHistories.Single(h => h.StudentId == _studentId);
        Assert.Equal(_downtownId, history.FromBranchId);
        Assert.Equal(_uptownId, history.ToBranchId);
        Assert.Equal("Family relocated", history.Reason);
    }

    [Fact]
    public async Task Handle_SameBranch_ThrowsBadRequest()
    {
        Seed();
        var handler = CreateHandler();

        await Assert.ThrowsAsync<BadRequestException>(
            () => handler.Handle(new TransferStudentBranchCommand(_studentId, _downtownId, null), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnknownDestinationBranch_ThrowsNotFound()
    {
        Seed();
        var handler = CreateHandler();

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new TransferStudentBranchCommand(_studentId, Guid.NewGuid(), null), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_BranchManagerWithoutAccessToCurrentBranch_ThrowsForbidden()
    {
        Seed();
        CurrentUser.UserId = Guid.NewGuid();
        CurrentUser.Roles = [RoleNames.BranchManager];
        CurrentUser.BranchIds = [_uptownId]; // manages Uptown, not the student's current branch (Downtown)

        var handler = CreateHandler();

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => handler.Handle(new TransferStudentBranchCommand(_studentId, _uptownId, null), CancellationToken.None));
    }
}

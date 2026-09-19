using CEMS.Application.Common.Exceptions;
using CEMS.Application.Students.Guardians.Commands.CreateGuardian;
using CEMS.Application.Students.Guardians.Commands.UpdateGuardian;
using CEMS.Application.Students.Guardians.Queries.GetGuardianById;
using CEMS.Application.Students.Guardians.Queries.GetGuardians;
using CEMS.Application.Tests.TestSupport;
using CEMS.Domain.Branches;
using CEMS.Domain.Students;
using CEMS.Domain.Users;

namespace CEMS.Application.Tests.Students;

/// <summary>
/// Guardians belong to no branch directly, so visibility is derived from their students. These tests pin
/// down that a Front Desk or Branch Manager can't read or edit another branch's guardians.
/// </summary>
public class GuardianHandlerTests : SeededHandlerTestBase
{
    private readonly Branch _smouha;
    private readonly Branch _kafrAbdo;
    private readonly Guardian _smouhaParent;
    private readonly Guardian _kafrAbdoParent;
    private readonly Guardian _sharedParent;
    private readonly Guardian _unlinkedParent;

    public GuardianHandlerTests()
    {
        _smouha = AddBranch("Smouha");
        _kafrAbdo = AddBranch("Kafr Abdo");
        _smouhaParent = AddGuardian("Smouha Parent");
        _kafrAbdoParent = AddGuardian("Kafr Abdo Parent");
        _sharedParent = AddGuardian("Shared Parent");
        _unlinkedParent = AddGuardian("Just Created");

        Link(AddStudent(_smouha, "S1"), _smouhaParent);
        Link(AddStudent(_kafrAbdo, "K1"), _kafrAbdoParent);
        Link(AddStudent(_smouha, "S2"), _sharedParent);
        Link(AddStudent(_kafrAbdo, "K2"), _sharedParent);
    }

    private void Link(Student student, Guardian guardian)
    {
        Context.StudentGuardians.Add(new StudentGuardian { StudentId = student.Id, GuardianId = guardian.Id, RelationshipType = RelationshipType.Father });
        Context.SaveChanges();
    }

    private async Task<List<string>> ListNames() =>
        (await new GetGuardiansQueryHandler(Context, CurrentUser).Handle(new GetGuardiansQuery(), CancellationToken.None))
            .Select(g => g.FullName).ToList();

    [Fact]
    public async Task List_OwnerSeesEveryGuardian()
    {
        ActAs(RoleNames.Owner);

        Assert.Equal(4, (await ListNames()).Count);
    }

    [Fact]
    public async Task List_BranchStaffSeeGuardiansOfTheirBranchesStudents_PlusUnlinkedOnes()
    {
        ActAs(RoleNames.FrontDesk, _smouha);

        var names = await ListNames();

        Assert.Equal(["Just Created", "Shared Parent", "Smouha Parent"], names);
        Assert.DoesNotContain("Kafr Abdo Parent", names);
    }

    [Fact]
    public async Task List_AGuardianWithChildrenAtBothBranchesIsVisibleToBoth()
    {
        ActAs(RoleNames.BranchManager, _kafrAbdo);

        Assert.Contains("Shared Parent", await ListNames());
    }

    [Fact]
    public async Task GetById_OwnBranchesGuardianIsReturned_OtherBranchesIsForbidden()
    {
        ActAs(RoleNames.FrontDesk, _smouha);
        var handler = new GetGuardianByIdQueryHandler(Context, CurrentUser);

        var own = await handler.Handle(new GetGuardianByIdQuery(_smouhaParent.Id), CancellationToken.None);

        Assert.Equal("Smouha Parent", own.FullName);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(new GetGuardianByIdQuery(_kafrAbdoParent.Id), CancellationToken.None));
    }

    [Fact]
    public async Task GetById_UnknownGuardian_ThrowsNotFound_AndATeacherIsForbidden()
    {
        ActAs(RoleNames.Owner);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetGuardianByIdQueryHandler(Context, CurrentUser).Handle(new GetGuardianByIdQuery(Guid.NewGuid()), CancellationToken.None));

        ActAs(RoleNames.Teacher, _smouha);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetGuardianByIdQueryHandler(Context, CurrentUser).Handle(new GetGuardianByIdQuery(_smouhaParent.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Update_OwnBranchesGuardian_ChangesContactDetails()
    {
        ActAs(RoleNames.FrontDesk, _smouha);

        var result = await new UpdateGuardianCommandHandler(Context, CurrentUser)
            .Handle(new UpdateGuardianCommand(_smouhaParent.Id, "Renamed", "01999999999", "new@example.com"), CancellationToken.None);

        Assert.Equal("Renamed", result.FullName);
        Assert.Equal("01999999999", Context.Guardians.Single(g => g.Id == _smouhaParent.Id).Phone);
    }

    [Fact]
    public async Task Update_AnotherBranchesGuardian_IsForbiddenAndChangesNothing()
    {
        ActAs(RoleNames.FrontDesk, _smouha);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new UpdateGuardianCommandHandler(Context, CurrentUser)
                .Handle(new UpdateGuardianCommand(_kafrAbdoParent.Id, "Hacked", "000", "x@y.z"), CancellationToken.None));

        Assert.Equal("Kafr Abdo Parent", Context.Guardians.Single(g => g.Id == _kafrAbdoParent.Id).FullName);
    }

    [Fact]
    public async Task Update_UnknownGuardian_ThrowsNotFound()
    {
        ActAs(RoleNames.Owner);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateGuardianCommandHandler(Context, CurrentUser)
                .Handle(new UpdateGuardianCommand(Guid.NewGuid(), "X", "1", "x@y.z"), CancellationToken.None));
    }

    [Fact]
    public async Task Create_PersistsTheGuardian_AndItIsImmediatelyVisibleToItsCreator()
    {
        ActAs(RoleNames.FrontDesk, _smouha);

        var dto = await new CreateGuardianCommandHandler(Context).Handle(
            new CreateGuardianCommand("Fresh Parent", "01100000009", "fresh@example.com"), CancellationToken.None);

        Assert.Contains("Fresh Parent", await ListNames());
        Assert.Equal(dto.Id, Context.Guardians.Single(g => g.FullName == "Fresh Parent").Id);
    }
}

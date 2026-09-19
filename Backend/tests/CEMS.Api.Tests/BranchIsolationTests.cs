using System.Net;
using System.Net.Http.Json;

namespace CEMS.Api.Tests;

/// <summary>
/// The headline claim of the whole project, proven over real HTTP with real signed-in users: staff at one
/// branch cannot see or change another branch's data, and access follows a student when they transfer.
/// </summary>
public class BranchIsolationTests : IClassFixture<ApiWorld>
{
    private readonly ApiWorld _w;

    public BranchIsolationTests(ApiWorld world) => _w = world;

    // ---- Students ----

    [Fact]
    public async Task StudentLists_ContainOnlyTheCallersOwnBranch_OwnerSeesBoth()
    {
        var smouha = await ApiWorld.IdsAsync(await _w.ManagerSmouha.GetAsync("/api/students"));
        var kafrAbdo = await ApiWorld.IdsAsync(await _w.FrontDeskKafrAbdo.GetAsync("/api/students"));
        var owner = await ApiWorld.IdsAsync(await _w.Owner.GetAsync("/api/students"));

        Assert.Contains(_w.TaughtStudent, smouha);
        Assert.DoesNotContain(_w.KafrAbdoStudent, smouha);
        Assert.Contains(_w.KafrAbdoStudent, kafrAbdo);
        Assert.DoesNotContain(_w.TaughtStudent, kafrAbdo);
        Assert.Contains(_w.TaughtStudent, owner);
        Assert.Contains(_w.KafrAbdoStudent, owner);
    }

    [Fact]
    public async Task ReadingAnotherBranchsStudent_IsForbidden_InEveryStudentScopedEndpoint()
    {
        foreach (var path in new[] { "", "/guardians", "/invoices", "/balance", "/enrollments", "/attendance", "/grades", "/branch-history", "/report-card" })
        {
            var response = await _w.ManagerSmouha.GetAsync($"/api/students/{_w.KafrAbdoStudent}{path}");
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"GET /api/students/{{kafr}}{path} returned {(int)response.StatusCode}");
        }
    }

    [Fact]
    public async Task WritingToAnotherBranch_IsForbidden()
    {
        var updateStudent = await _w.FrontDeskSmouha.PutAsJsonAsync($"/api/students/{_w.KafrAbdoStudent}",
            new { fullName = "Hijacked", dateOfBirth = "2013-04-12", gender = "Male", status = "Active" });
        var createStudentThere = await _w.ManagerSmouha.PostAsJsonAsync("/api/students", new
        {
            fullName = "Sneaky", dateOfBirth = "2013-04-12", gender = "Male", branchId = _w.KafrAbdo,
            existingGuardianId = _w.SmouhaGuardian, relationshipType = "Father", isPrimaryContact = true
        });
        var createRoomThere = await _w.ManagerSmouha.PostAsJsonAsync($"/api/branches/{_w.KafrAbdo}/rooms", new { name = "Ghost", capacity = 5 });
        var editRoomThere = await _w.ManagerSmouha.PutAsJsonAsync($"/api/rooms/{_w.KafrAbdoRoom}", new { name = "Hijacked", capacity = 1 });
        var deleteStudent = await _w.FrontDeskSmouha.DeleteAsync($"/api/students/{_w.KafrAbdoStudent}");

        Assert.All(new[] { updateStudent, createStudentThere, createRoomThere, editRoomThere, deleteStudent },
            r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
    }

    [Fact]
    public async Task BranchListsAndDetails_AreScoped()
    {
        var managerBranches = await ApiWorld.IdsAsync(await _w.ManagerSmouha.GetAsync("/api/branches"));
        var ownerBranches = await ApiWorld.IdsAsync(await _w.Owner.GetAsync("/api/branches"));

        Assert.Equal([_w.Smouha], managerBranches);
        Assert.Contains(_w.KafrAbdo, ownerBranches);
        Assert.Equal(HttpStatusCode.Forbidden, (await _w.ManagerSmouha.GetAsync($"/api/branches/{_w.KafrAbdo}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _w.ManagerSmouha.GetAsync($"/api/branches/{_w.KafrAbdo}/rooms")).StatusCode);
    }

    // ---- Guardians (visible only through their students) ----

    [Fact]
    public async Task Guardians_AreVisibleOnlyThroughStudentsAtTheCallersBranch()
    {
        var smouhaView = await ApiWorld.IdsAsync(await _w.FrontDeskSmouha.GetAsync("/api/guardians"));
        var kafrAbdoView = await ApiWorld.IdsAsync(await _w.FrontDeskKafrAbdo.GetAsync("/api/guardians"));

        Assert.Contains(_w.SmouhaGuardian, smouhaView);
        Assert.DoesNotContain(_w.KafrAbdoGuardian, smouhaView);
        Assert.Contains(_w.KafrAbdoGuardian, kafrAbdoView);
        Assert.DoesNotContain(_w.SmouhaGuardian, kafrAbdoView);
    }

    [Fact]
    public async Task AnotherBranchsGuardian_CannotBeReadEditedOrLinked()
    {
        var read = await _w.FrontDeskSmouha.GetAsync($"/api/guardians/{_w.KafrAbdoGuardian}");
        var edit = await _w.FrontDeskSmouha.PutAsJsonAsync($"/api/guardians/{_w.KafrAbdoGuardian}",
            new { fullName = "Hijacked", phone = "000", email = "x@y.z" });
        var link = await _w.FrontDeskSmouha.PostAsJsonAsync($"/api/students/{_w.TaughtStudent}/guardians",
            new { guardianId = _w.KafrAbdoGuardian, relationshipType = "Mother", isPrimaryContact = false });

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, link.StatusCode);   // reported as not found so linking can't probe other branches
    }

    // ---- Teachers see only what they teach ----

    [Fact]
    public async Task ATeacher_CanOpenAStudentTheyTeach_ButNotAnotherStudentAtTheSameBranch()
    {
        Assert.Equal(HttpStatusCode.OK, (await _w.Teacher.GetAsync($"/api/students/{_w.TaughtStudent}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _w.Teacher.GetAsync($"/api/students/{_w.UntaughtStudent}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _w.Teacher.GetAsync($"/api/students/{_w.KafrAbdoStudent}")).StatusCode);
    }

    // ---- Transfers: access follows the student ----

    [Fact]
    public async Task AfterATransfer_TheOldBranchLosesAccessAndTheNewBranchGainsIt()
    {
        var student = await _w.CreateStudentAsync("Mover", _w.Smouha, _w.SmouhaGuardian);
        Assert.Equal(HttpStatusCode.OK, (await _w.ManagerSmouha.GetAsync($"/api/students/{student}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _w.ManagerKafrAbdo.GetAsync($"/api/students/{student}")).StatusCode);

        var transfer = await _w.FrontDeskSmouha.PostAsJsonAsync($"/api/students/{student}/transfer-branch",
            new { newBranchId = _w.KafrAbdo, reason = "Family relocated" });
        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await _w.ManagerSmouha.GetAsync($"/api/students/{student}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _w.ManagerKafrAbdo.GetAsync($"/api/students/{student}")).StatusCode);

        var history = await ApiWorld.ReadJsonAsync(await _w.ManagerKafrAbdo.GetAsync($"/api/students/{student}/branch-history"));
        var entry = Assert.Single(history.EnumerateArray());
        Assert.Equal("Family relocated", entry.GetProperty("reason").GetString());
        Assert.Equal("CodeCamp Smouha", entry.GetProperty("fromBranchName").GetString());
    }

    // ---- Analytics ----

    [Fact]
    public async Task Analytics_AManagerMustNameTheirOwnBranch_AndCannotAskForAnotherOrForEverything()
    {
        const string window = "periodStart=2030-01-01&periodEnd=2030-01-31";

        Assert.Equal(HttpStatusCode.OK, (await _w.ManagerSmouha.GetAsync($"/api/analytics/revenue?branchId={_w.Smouha}&{window}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _w.ManagerSmouha.GetAsync($"/api/analytics/revenue?branchId={_w.KafrAbdo}&{window}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _w.ManagerSmouha.GetAsync($"/api/analytics/revenue?{window}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _w.Owner.GetAsync($"/api/analytics/revenue?{window}")).StatusCode);
    }
}

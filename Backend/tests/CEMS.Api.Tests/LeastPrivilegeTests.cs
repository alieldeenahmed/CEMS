using System.Net.Http.Json;
using System.Text.Json;

namespace CEMS.Api.Tests;

/// <summary>Data a role has no need for is not sent to it at all, rather than hidden by the UI.</summary>
public class LeastPrivilegeTests : IClassFixture<ApiWorld>
{
    private readonly ApiWorld _w;

    public LeastPrivilegeTests(ApiWorld world) => _w = world;

    private static bool PayIsHidden(JsonElement teacher) =>
        teacher.GetProperty("payType").ValueKind == JsonValueKind.Null && teacher.GetProperty("payRate").ValueKind == JsonValueKind.Null;

    [Fact]
    public async Task TheFrontDesk_SeesWhoTeachesWhere_ButNotWhatTheyArePaid()
    {
        var list = await _w.FrontDeskSmouha.GetFromJsonAsync<JsonElement>("/api/teachers");
        var detail = await _w.FrontDeskSmouha.GetFromJsonAsync<JsonElement>($"/api/teachers/{_w.TeacherId}");

        Assert.Equal("Ahmed Nabil", detail.GetProperty("fullName").GetString());
        Assert.True(PayIsHidden(detail));
        Assert.All(list.EnumerateArray(), teacher => Assert.True(PayIsHidden(teacher)));
    }

    [Fact]
    public async Task TheOwnerAndTheBranchManager_SeePay_AndSoDoesTheTeacherAboutThemselves()
    {
        foreach (var client in new[] { _w.Owner, _w.ManagerSmouha, _w.Teacher })
        {
            var detail = await client.GetFromJsonAsync<JsonElement>($"/api/teachers/{_w.TeacherId}");

            Assert.Equal("Hourly", detail.GetProperty("payType").GetString());
            Assert.Equal(150m, detail.GetProperty("payRate").GetDecimal());
        }

        var mine = await _w.Teacher.GetFromJsonAsync<JsonElement>("/api/teachers/my-profile");
        Assert.Equal(150m, mine.GetProperty("payRate").GetDecimal());
    }
}

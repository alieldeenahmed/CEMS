using System.Net;
using System.Net.Http.Json;
using CEMS.Infrastructure.Auth;

namespace CEMS.Api.Tests;

/// <summary>
/// A JWT is valid until it expires, so the API re-checks the account on every request; and repeated wrong
/// passwords lock an account. Both close the gap between "this token was once valid" and "this person is
/// allowed in now".
/// </summary>
public class AccountLifecycleTests : IClassFixture<ApiWorld>
{
    private readonly ApiWorld _w;

    public AccountLifecycleTests(ApiWorld world) => _w = world;

    private async Task<(Guid UserId, string Email)> NewFrontDeskAsync()
    {
        var email = $"fd.{Guid.NewGuid():N}@codecamp.demo";
        var response = await _w.Owner.PostAsJsonAsync("/api/users/staff",
            new { email, password = ApiWorld.Password, fullName = "Temp Desk", phoneNumber = "01000000000", role = "FrontDesk", branchId = _w.Smouha });
        response.EnsureSuccessStatusCode();
        return ((await ApiWorld.ReadJsonAsync(response)).GetProperty("userId").GetGuid(), email);
    }

    private Task<HttpResponseMessage> SetActive(Guid userId, bool active) =>
        _w.Owner.PutAsJsonAsync($"/api/users/staff/{userId}/status", new { isActive = active });

    [Fact]
    public async Task ADeactivatedAccount_LosesAccessImmediately_ItsExistingTokenNoLongerWorks()
    {
        var (userId, email) = await NewFrontDeskAsync();
        var session = await _w.SignInAsync(email);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/students")).StatusCode);

        (await SetActive(userId, false)).EnsureSuccessStatusCode();

        // The token has hours left on it and its signature is still perfect -- but the account is switched off.
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/students")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/branches")).StatusCode);
    }

    [Fact]
    public async Task ReactivatingTheAccount_RestoresAccessWithTheSameToken()
    {
        var (userId, email) = await NewFrontDeskAsync();
        var session = await _w.SignInAsync(email);
        await SetActive(userId, false);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/students")).StatusCode);

        await SetActive(userId, true);

        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/students")).StatusCode);
    }

    [Fact]
    public async Task ADeactivatedAccount_CannotLogInAgain()
    {
        var (userId, email) = await NewFrontDeskAsync();
        await SetActive(userId, false);

        var response = await _w.Anonymous.PostAsJsonAsync("/api/auth/login", new { email, password = ApiWorld.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ATokenForAnAccountThatNoLongerExists_IsRejected()
    {
        // Correctly signed, unexpired, right issuer and audience -- for a user id that is not in the database.
        var token = AuthenticationHelpers.ForgeToken(CemsApiFactory.SigningKey, roles: ["Owner"]);
        var client = _w.Authorized(token);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/students")).StatusCode);
    }

    [Fact]
    public async Task TheRolesInTheTokenAreNotTrusted_TheAccountsCurrentRolesAre()
    {
        var (userId, _) = await NewFrontDeskAsync();
        // A validly signed token that claims Owner for a user who is really a front-desk clerk.
        var token = AuthenticationHelpers.ForgeToken(CemsApiFactory.SigningKey, subject: userId, roles: ["Owner"]);
        var client = _w.Authorized(token);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users/staff")).StatusCode);   // Owner-only
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/students")).StatusCode);            // what a front desk may do
    }

    [Fact]
    public async Task TheBranchesInTheTokenAreNotTrusted_TheAccountsCurrentBranchesAre()
    {
        var (userId, _) = await NewFrontDeskAsync();   // assigned to Smouha only
        var token = AuthenticationHelpers.ForgeToken(CemsApiFactory.SigningKey, subject: userId, roles: ["FrontDesk"], branchIds: _w.KafrAbdo.ToString());
        var client = _w.Authorized(token);

        // The token claims Kafr Abdo; the account belongs to Smouha.
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/students/{_w.KafrAbdoStudent}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/students/{_w.TaughtStudent}")).StatusCode);
    }

    // ---- Lockout ----

    [Fact]
    public async Task FiveWrongPasswords_LockTheAccount_EvenForTheCorrectPasswordAfterwards()
    {
        var (_, email) = await NewFrontDeskAsync();

        for (var i = 0; i < 5; i++)
        {
            var wrong = await _w.Anonymous.PostAsJsonAsync("/api/auth/login", new { email, password = "not-the-password" });
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        var right = await _w.Anonymous.PostAsJsonAsync("/api/auth/login", new { email, password = ApiWorld.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, right.StatusCode);
        // ...and it says the same thing a wrong password does, so it does not reveal that the lock exists.
        var body = await right.Content.ReadAsStringAsync();
        Assert.Contains("Invalid email or password", body);
    }

    [Fact]
    public async Task FewerThanFiveFailures_AreForgivenOnceThePasswordIsRight()
    {
        var (_, email) = await NewFrontDeskAsync();

        for (var i = 0; i < 4; i++)
        {
            await _w.Anonymous.PostAsJsonAsync("/api/auth/login", new { email, password = "nope" });
        }

        Assert.Equal(HttpStatusCode.OK, (await _w.Anonymous.PostAsJsonAsync("/api/auth/login", new { email, password = ApiWorld.Password })).StatusCode);

        // A successful login resets the counter: four more wrong guesses still do not lock it.
        for (var i = 0; i < 4; i++)
        {
            await _w.Anonymous.PostAsJsonAsync("/api/auth/login", new { email, password = "nope" });
        }

        Assert.Equal(HttpStatusCode.OK, (await _w.Anonymous.PostAsJsonAsync("/api/auth/login", new { email, password = ApiWorld.Password })).StatusCode);
    }

    // ---- Configuration ----

    [Theory]
    [InlineData("short-key", "CEMS", "CEMS", 60, "Jwt:Key")]
    [InlineData("a-key-that-is-long-enough-for-hs256-signing", "", "CEMS", 60, "Jwt:Issuer")]
    [InlineData("a-key-that-is-long-enough-for-hs256-signing", "CEMS", "", 60, "Jwt:Audience")]
    [InlineData("a-key-that-is-long-enough-for-hs256-signing", "CEMS", "CEMS", 0, "Jwt:ExpiryMinutes")]
    [InlineData("a-key-that-is-long-enough-for-hs256-signing", "CEMS", "CEMS", 100000, "Jwt:ExpiryMinutes")]
    public void InvalidJwtSettings_AreReported_SoTheAppRefusesToStart(string key, string issuer, string audience, int minutes, string expectedProblem)
    {
        var problems = new JwtSettings { Key = key, Issuer = issuer, Audience = audience, ExpiryMinutes = minutes }.Validate();

        Assert.Contains(problems, p => p.Contains(expectedProblem));
    }

    [Fact]
    public void ValidJwtSettings_HaveNoProblems()
    {
        var problems = new JwtSettings { Key = new string('k', 32), Issuer = "CEMS", Audience = "CEMS", ExpiryMinutes = 360 }.Validate();

        Assert.Empty(problems);
    }

    [Fact]
    public void TheKeyLengthIsMeasuredInBytes_NotCharacters()
    {
        // 31 two-byte characters is 62 bytes: long enough. 31 ASCII characters is 31 bytes: too short.
        Assert.Empty(new JwtSettings { Key = new string('é', 31), Issuer = "a", Audience = "a" }.Validate());
        Assert.Contains(new JwtSettings { Key = new string('k', 31), Issuer = "a", Audience = "a" }.Validate(), p => p.Contains("Jwt:Key"));
    }
}

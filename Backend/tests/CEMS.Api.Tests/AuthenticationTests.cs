using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace CEMS.Api.Tests;

/// <summary>Sign-in, the one-time owner bootstrap, and what the JWT pipeline accepts and rejects.</summary>
public class AuthenticationTests
{
    private static async Task<CemsApiFactory> NewAppAsync()
    {
        var app = new CemsApiFactory();
        await app.InitializeAsync();
        return app;
    }

    private static string ForgeToken(string key, string issuer = "CEMS", string audience = "CEMS", DateTime? expires = null, Guid? subject = null, params string[] roles)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, (subject ?? Guid.NewGuid()).ToString()) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        var token = new JwtSecurityToken(issuer, audience, claims,
            expires: expires ?? DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static HttpClient WithToken(CemsApiFactory app, string token)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ---- Anonymous access ----

    [Theory]
    [InlineData("GET", "/api/students")]
    [InlineData("GET", "/api/branches")]
    [InlineData("GET", "/api/curricula")]
    [InlineData("GET", "/api/analytics/revenue?periodStart=2030-01-01&periodEnd=2030-01-31")]
    [InlineData("POST", "/api/branches")]
    [InlineData("DELETE", "/api/students/00000000-0000-0000-0000-000000000001")]
    public async Task ProtectedEndpoints_RejectAnonymousRequestsWith401(string method, string path)
    {
        using var app = await NewAppAsync();

        var response = await app.CreateClient().SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Login ----

    [Fact]
    public async Task Login_WithTheRightPassword_ReturnsATokenCarryingTheRoleAndBranch()
    {
        using var app = new ApiWorld();
        await app.InitializeAsync();

        var response = await app.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "bm.smouha@codecamp.demo", password = CemsApiFactory.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await CemsApiFactory.ReadJsonAsync(response);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.GetProperty("token").GetString());
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Role && c.Value == "BranchManager");
        Assert.Contains(jwt.Claims, c => c.Type == "branch_id" && c.Value == app.Smouha.ToString());
        Assert.Equal("Nourhan Adel", body.GetProperty("fullName").GetString());
    }

    [Theory]
    [InlineData("owner@codecamp.demo", "WrongPassword1")]
    [InlineData("nobody@codecamp.demo", "DemoPass123")]
    public async Task Login_WithBadCredentials_Returns401WithoutSayingWhichPartWasWrong(string email, string password)
    {
        using var app = await NewAppAsync();
        await app.SignInAsOwnerAsync();

        var response = await app.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await CemsApiFactory.ReadJsonAsync(response);
        Assert.Equal("Invalid email or password.", problem.GetProperty("title").GetString());
        Assert.True(problem.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Login_WithMalformedInput_Returns400WithFieldErrors()
    {
        using var app = await NewAppAsync();

        var response = await app.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "not-an-email", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await CemsApiFactory.ReadJsonAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("Email", out _));
        Assert.True(errors.TryGetProperty("Password", out _));
    }

    [Fact]
    public async Task ADeactivatedAccount_CanNoLongerLogIn()
    {
        using var app = new ApiWorld();
        await app.InitializeAsync();

        var deactivate = await app.Owner.PutAsJsonAsync($"/api/users/staff/{app.TeacherUserId}/status", new { isActive = false });
        var login = await app.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "teacher.smouha@codecamp.demo", password = CemsApiFactory.Password });

        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    // ---- The one-time owner bootstrap ----

    [Fact]
    public async Task Bootstrap_CreatesTheOwnerOnce_ThenClosesForGood()
    {
        using var app = await NewAppAsync();
        var body = new { email = "owner@codecamp.demo", password = "DemoPass123", fullName = "Mostafa", phoneNumber = "0100" };

        var first = await app.CreateClient().PostAsJsonAsync("/api/auth/bootstrap-owner", body);
        var second = await app.CreateClient().PostAsJsonAsync("/api/auth/bootstrap-owner", body with { email = "attacker@evil.test" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var roles = (await CemsApiFactory.ReadJsonAsync(first)).GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToArray();
        Assert.Equal(["Owner"], roles);
        Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);
    }

    // ---- What the JWT pipeline accepts ----

    [Fact]
    public async Task AWellFormedToken_ForARealAccount_IsAccepted_AndItsRolesAreHonoured()
    {
        using var app = await NewAppAsync();
        var ownerClient = await app.SignInAsOwnerAsync();
        var ownerId = await FirstUserIdAsync(app);
        var branch = (await CemsApiFactory.ReadJsonAsync(await ownerClient.PostAsJsonAsync("/api/branches",
            new { name = "Smouha", address = "14 Fawzy Moaz St", phone = "034567001" }))).GetProperty("id").GetGuid();
        var deskUser = (await CemsApiFactory.ReadJsonAsync(await ownerClient.PostAsJsonAsync("/api/users/staff",
            new { email = "desk@codecamp.demo", password = CemsApiFactory.Password, fullName = "Desk", phoneNumber = "0100", role = "FrontDesk", branchId = branch })))
            .GetProperty("userId").GetGuid();

        var owner = WithToken(app, ForgeToken(CemsApiFactory.SigningKey, subject: ownerId, roles: "Owner"));
        var frontDesk = WithToken(app, ForgeToken(CemsApiFactory.SigningKey, subject: deskUser, roles: "FrontDesk"));

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/students")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await frontDesk.GetAsync("/api/users/staff")).StatusCode);
    }

    private static async Task<Guid> FirstUserIdAsync(CemsApiFactory app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CEMS.Infrastructure.Persistence.ApplicationDbContext>();
        return await context.Users.Select(u => u.Id).FirstAsync();
    }

    [Fact]
    public async Task ATokenThatIsExpired_SignedWithTheWrongKey_ForAnotherIssuerOrAudience_OrGarbage_IsRejected()
    {
        using var app = await NewAppAsync();
        var rejected = new[]
        {
            ForgeToken(CemsApiFactory.SigningKey, expires: DateTime.UtcNow.AddMinutes(-1), roles: "Owner"),
            ForgeToken("a-different-signing-key-that-is-also-long-enough-xyz", roles: "Owner"),
            ForgeToken(CemsApiFactory.SigningKey, issuer: "someone-else", roles: "Owner"),
            ForgeToken(CemsApiFactory.SigningKey, audience: "another-app", roles: "Owner"),
            "not-a-jwt"
        };

        foreach (var token in rejected)
        {
            var response = await WithToken(app, token).GetAsync("/api/students");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task ATokenWithOnlyTheOwnersNameInThePayload_ButNoValidSignature_IsRejected()
    {
        using var app = await NewAppAsync();
        var real = ForgeToken(CemsApiFactory.SigningKey, roles: "FrontDesk");
        var parts = real.Split('.');
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"http://schemas.microsoft.com/ws/2008/06/identity/claims/role\":\"Owner\",\"iss\":\"CEMS\",\"aud\":\"CEMS\"}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var response = await WithToken(app, $"{parts[0]}.{payload}.{parts[2]}").GetAsync("/api/users/staff");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

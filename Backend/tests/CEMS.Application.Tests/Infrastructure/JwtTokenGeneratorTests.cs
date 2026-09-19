using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Users;
using CEMS.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CEMS.Application.Tests.Infrastructure;

public class JwtTokenGeneratorTests
{
    private const string Key = "a-test-signing-key-that-is-long-enough-for-hmac-sha256";

    private static readonly JwtSettings Settings = new() { Key = Key, Issuer = "CEMS", Audience = "CEMS", ExpiryMinutes = 360 };

    private static AuthenticatedUser Manager(params Guid[] branches) =>
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "nourhan@codecamp.demo", "Nourhan Adel", [RoleNames.BranchManager], branches, true);

    private static JwtTokenGenerator Generator(JwtSettings? settings = null) => new(Options.Create(settings ?? Settings));

    private static JwtSecurityToken Read(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact]
    public void Token_CarriesTheIdentityRoleAndEveryBranchAsClaims()
    {
        var smouha = Guid.NewGuid();
        var kafrAbdo = Guid.NewGuid();

        var (token, _) = Generator().GenerateToken(Manager(smouha, kafrAbdo));
        var jwt = Read(token);

        Assert.Equal("22222222-2222-2222-2222-222222222222", jwt.Subject);
        Assert.Equal("nourhan@codecamp.demo", jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("Nourhan Adel", jwt.Claims.Single(c => c.Type == "full_name").Value);
        Assert.Equal([RoleNames.BranchManager], jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray());
        Assert.Equivalent(new[] { smouha.ToString(), kafrAbdo.ToString() }, jwt.Claims.Where(c => c.Type == "branch_id").Select(c => c.Value).ToArray());
    }

    [Fact]
    public void Token_ForAnOwnerWithNoBranches_HasNoBranchClaims()
    {
        var owner = new AuthenticatedUser(Guid.NewGuid(), "owner@codecamp.demo", "Mostafa", [RoleNames.Owner], Array.Empty<Guid>(), true);

        var jwt = Read(Generator().GenerateToken(owner).Token);

        Assert.DoesNotContain(jwt.Claims, c => c.Type == "branch_id");
    }

    [Fact]
    public void Token_ExpiresAfterTheConfiguredNumberOfMinutes_AndReportsThatSameInstant()
    {
        var before = DateTime.UtcNow;

        var (token, expiresAtUtc) = Generator().GenerateToken(Manager());
        var jwt = Read(token);

        Assert.InRange(expiresAtUtc, before.AddMinutes(360), DateTime.UtcNow.AddMinutes(360));
        Assert.Equal(expiresAtUtc, jwt.ValidTo, TimeSpan.FromSeconds(1));
    }

    private static TokenValidationParameters Validation(string key, string issuer = "CEMS", string audience = "CEMS") => new()
    {
        ValidateIssuer = true, ValidIssuer = issuer,
        ValidateAudience = true, ValidAudience = audience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ValidateLifetime = true, ClockSkew = TimeSpan.Zero
    };

    [Fact]
    public void Token_ValidatesWithTheSameKeyIssuerAndAudience_AndWithNoOtherConfiguration()
    {
        var (token, _) = Generator().GenerateToken(Manager());

        new JwtSecurityTokenHandler().ValidateToken(token, Validation(Key), out var validated);

        Assert.NotNull(validated);
    }

    [Theory]
    [InlineData("a-completely-different-signing-key-of-sufficient-length", "CEMS", "CEMS")]
    [InlineData(Key, "someone-else", "CEMS")]
    [InlineData(Key, "CEMS", "another-app")]
    public void Token_IsRejectedWhenTheKeyIssuerOrAudienceDoesNotMatch(string key, string issuer, string audience)
    {
        var (token, _) = Generator().GenerateToken(Manager());

        Assert.ThrowsAny<SecurityTokenException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(token, Validation(key, issuer, audience), out _));
    }

    [Fact]
    public void Token_IsRejectedOnceExpired()
    {
        var expired = new JwtSettings { Key = Key, Issuer = "CEMS", Audience = "CEMS", ExpiryMinutes = -5 };
        var (token, _) = Generator(expired).GenerateToken(Manager());

        Assert.ThrowsAny<SecurityTokenException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(token, Validation(Key), out _));
    }

    [Fact]
    public void Token_TamperedWithAfterSigning_IsRejected()
    {
        var (token, _) = Generator().GenerateToken(Manager());
        var parts = token.Split('.');
        var forgedPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"role\":\"Owner\"}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var forged = $"{parts[0]}.{forgedPayload}.{parts[2]}";

        Assert.ThrowsAny<SecurityTokenException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(forged, Validation(Key), out _));
    }
}

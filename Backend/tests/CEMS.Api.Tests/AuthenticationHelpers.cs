using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace CEMS.Api.Tests;

public static class AuthenticationHelpers
{
    /// <summary>A token signed with <paramref name="key"/>, for testing what the pipeline accepts and rejects.</summary>
    public static string ForgeToken(
        string key, string issuer = "CEMS", string audience = "CEMS", DateTime? expires = null, Guid? subject = null,
        string? branchIds = null, string[]? roles = null)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, (subject ?? Guid.NewGuid()).ToString()) };
        claims.AddRange((roles ?? []).Select(r => new Claim(ClaimTypes.Role, r)));
        if (branchIds is not null)
        {
            claims.AddRange(branchIds.Split(',').Select(b => new Claim("branch_id", b)));
        }

        var token = new JwtSecurityToken(issuer, audience, claims,
            expires: expires ?? DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

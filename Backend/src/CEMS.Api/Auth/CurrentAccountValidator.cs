using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CEMS.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CEMS.Api.Auth;

/// <summary>
/// Runs after a bearer token's signature and lifetime check out. JWTs are self-contained, so on their own they
/// cannot be revoked; this makes the account itself the source of truth on every request:
///  * a deleted or deactivated account is rejected immediately, not when its token happens to expire;
///  * the roles and branches used for authorization are the account's current ones, not the ones frozen into
///    the token at login.
/// The cost is one small database lookup per authenticated request, which is the right trade for a system this
/// size (a token blacklist or short-lived tokens with refresh would be the next step if it grew).
/// </summary>
public static class CurrentAccountValidator
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var identity = principal?.Identity as ClaimsIdentity;

        if (identity is null || !Guid.TryParse(principal!.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
        {
            context.Fail("The token has no valid subject.");
            return;
        }

        var identityService = context.HttpContext.RequestServices.GetRequiredService<IIdentityService>();

        AuthenticatedUser account;
        try
        {
            account = await identityService.GetAuthenticatedUserAsync(userId);
        }
        catch (InvalidOperationException)
        {
            context.Fail("The account no longer exists.");
            return;
        }

        if (!account.IsActive)
        {
            context.Fail("The account is deactivated.");
            return;
        }

        foreach (var claim in identity.FindAll(ClaimTypes.Role).Concat(identity.FindAll("branch_id")).ToList())
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaims(account.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        identity.AddClaims(account.BranchIds.Select(branchId => new Claim("branch_id", branchId.ToString())));
    }
}

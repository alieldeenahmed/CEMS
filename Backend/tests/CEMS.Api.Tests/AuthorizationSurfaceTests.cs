using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace CEMS.Api.Tests;

/// <summary>
/// Reads the authorization policy of every controller action straight from its attributes. Two jobs:
/// nothing may be publicly reachable except login and the one-time owner bootstrap, and the exact role
/// list of every endpoint is pinned in <c>authorization-surface.txt</c>, so a role can't be widened by
/// accident in a refactor. A deliberate change is made by reviewing the diff this test prints and then
/// regenerating the file with <c>UPDATE_AUTH_SNAPSHOT=1</c>.
/// </summary>
public class AuthorizationSurfaceTests
{
    private record Endpoint(string Verb, string Route, string Roles, bool Anonymous)
    {
        public string Line => $"{Verb,-6} {Route,-62} {(Anonymous ? "ANONYMOUS" : Roles)}";
    }

    private static IEnumerable<Endpoint> Endpoints()
    {
        var controllers = typeof(Program).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var controller in controllers)
        {
            var classRoute = controller.GetCustomAttribute<RouteAttribute>()?.Template ?? "";
            var classRoles = controller.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Roles).FirstOrDefault(r => r is not null);
            var classAnonymous = controller.GetCustomAttribute<AllowAnonymousAttribute>() is not null;

            foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                foreach (var http in action.GetCustomAttributes<HttpMethodAttribute>())
                {
                    var template = http.Template ?? "";
                    var route = template.StartsWith("api/", StringComparison.Ordinal)
                        ? "/" + template
                        : "/" + string.Join('/', new[] { classRoute, template }.Where(p => p.Length > 0));

                    var roles = action.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Roles).FirstOrDefault(r => r is not null) ?? classRoles;
                    var anonymous = classAnonymous || action.GetCustomAttribute<AllowAnonymousAttribute>() is not null;

                    yield return new Endpoint(http.HttpMethods.Single(), route, roles is null ? "(any signed-in user)" : Normalize(roles), anonymous);
                }
            }
        }
    }

    private static string Normalize(string roles) => string.Join(",", roles.Split(',').Select(r => r.Trim()).OrderBy(r => r));

    private static string[] CurrentSurface() =>
        Endpoints().OrderBy(e => e.Route, StringComparer.Ordinal).ThenBy(e => e.Verb, StringComparer.Ordinal).Select(e => e.Line).ToArray();

    private static string SnapshotSourcePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CEMS.Api.Tests.csproj")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Could not locate the test project directory."), "authorization-surface.txt");
    }

    [Fact]
    public void OnlyLoginAndTheOwnerBootstrapAreAnonymous()
    {
        var anonymous = Endpoints().Where(e => e.Anonymous).Select(e => $"{e.Verb} {e.Route}").OrderBy(x => x).ToArray();

        Assert.Equal(["POST /api/auth/bootstrap-owner", "POST /api/auth/login"], anonymous);
    }

    [Fact]
    public void EveryEndpointFallsUnderTheGlobalAuthenticatedUserRequirement_SoNoneIsOpenByOmission()
    {
        // Program.cs adds an AuthorizeFilter requiring an authenticated user to every controller, so an
        // action with no [Authorize] of its own is still protected. What must never appear is an action
        // that opts out: the anonymous check above covers that.
        var withoutRoles = Endpoints().Where(e => !e.Anonymous && e.Roles == "(any signed-in user)").Select(e => $"{e.Verb} {e.Route}").ToArray();

        Assert.Equal(["GET /api/curricula", "GET /api/curricula/{id:guid}"], withoutRoles.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void TheRoleListOfEveryEndpointMatchesTheApprovedSnapshot()
    {
        var actual = CurrentSurface();

        if (Environment.GetEnvironmentVariable("UPDATE_AUTH_SNAPSHOT") == "1")
        {
            File.WriteAllLines(SnapshotSourcePath(), actual);
            return;
        }

        var approved = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "authorization-surface.txt"));

        var added = actual.Except(approved).ToArray();
        var removed = approved.Except(actual).ToArray();

        Assert.True(added.Length == 0 && removed.Length == 0,
            "The authorization surface changed. If this is intended, review it and regenerate the snapshot with UPDATE_AUTH_SNAPSHOT=1.\n"
            + "New or changed:\n  " + string.Join("\n  ", added) + "\nRemoved or previous:\n  " + string.Join("\n  ", removed));
    }

    [Fact]
    public void ThereAre114Endpoints()
    {
        Assert.Equal(114, Endpoints().Count());
    }
}

using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Application.Tests.TestSupport;
using CEMS.Application.Users.Commands.BootstrapOwner;
using CEMS.Application.Users.Commands.Login;
using CEMS.Domain.Users;

namespace CEMS.Application.Tests.Users;

public class AuthenticationHandlerTests : HandlerTestBase
{
    private class StubTokenGenerator : IJwtTokenGenerator
    {
        public AuthenticatedUser? LastUser { get; private set; }

        public (string Token, DateTime ExpiresAtUtc) GenerateToken(AuthenticatedUser user)
        {
            LastUser = user;
            return ($"token-for-{user.Email}", new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        }
    }

    private readonly TestIdentityService _identity = new();
    private readonly StubTokenGenerator _tokens = new();

    private static AuthenticatedUser User(string email, string role) =>
        new(Guid.NewGuid(), email, "Test User", [role], Array.Empty<Guid>(), true);

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokenAndProfile()
    {
        var user = User("owner@codecamp.demo", RoleNames.Owner);
        _identity.AddUser(user, "DemoPass123");

        var result = await new LoginCommandHandler(_identity, _tokens)
            .Handle(new LoginCommand("owner@codecamp.demo", "DemoPass123"), CancellationToken.None);

        Assert.Equal(user.UserId, result.UserId);
        Assert.Equal("token-for-owner@codecamp.demo", result.Token);
        Assert.Contains(RoleNames.Owner, result.Roles);
        Assert.Equal(user, _tokens.LastUser);
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsAuthenticationExceptionWithoutIssuingAToken()
    {
        _identity.AddUser(User("owner@codecamp.demo", RoleNames.Owner), "DemoPass123");

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            new LoginCommandHandler(_identity, _tokens).Handle(new LoginCommand("owner@codecamp.demo", "nope"), CancellationToken.None));

        Assert.Null(_tokens.LastUser);
    }

    [Fact]
    public async Task Login_UnknownEmail_GivesTheSameErrorAsAWrongPassword()
    {
        _identity.AddUser(User("owner@codecamp.demo", RoleNames.Owner), "DemoPass123");
        var handler = new LoginCommandHandler(_identity, _tokens);

        var unknown = await Assert.ThrowsAsync<AuthenticationException>(() =>
            handler.Handle(new LoginCommand("ghost@codecamp.demo", "DemoPass123"), CancellationToken.None));
        var wrongPassword = await Assert.ThrowsAsync<AuthenticationException>(() =>
            handler.Handle(new LoginCommand("owner@codecamp.demo", "nope"), CancellationToken.None));

        // An attacker must not be able to tell "no such account" from "wrong password".
        Assert.Equal(wrongPassword.Message, unknown.Message);
    }

    [Fact]
    public async Task BootstrapOwner_OnAnEmptySystem_CreatesTheOwnerAndReturnsAToken()
    {
        var result = await new BootstrapOwnerCommandHandler(Context, _identity, _tokens)
            .Handle(new BootstrapOwnerCommand("owner@codecamp.demo", "DemoPass123", "Mostafa El-Sayed", "01012340001"), CancellationToken.None);

        Assert.Equal(("owner@codecamp.demo", RoleNames.Owner), Assert.Single(_identity.CreatedUsers));
        Assert.Equal("token-for-owner@codecamp.demo", result.Token);
    }

    [Fact]
    public async Task BootstrapOwner_OnceAnyUserExists_IsForbidden()
    {
        _identity.AddUser(User("someone@codecamp.demo", RoleNames.FrontDesk));

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new BootstrapOwnerCommandHandler(Context, _identity, _tokens)
                .Handle(new BootstrapOwnerCommand("attacker@evil.test", "Password123", "Attacker", "0100"), CancellationToken.None));

        Assert.Empty(_identity.CreatedUsers);
    }

    [Fact]
    public async Task BootstrapOwner_WhenIdentityRejectsTheUser_ThrowsBadRequestWithItsErrors()
    {
        _identity.CreateUserErrors = ["Password is too weak."];

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            new BootstrapOwnerCommandHandler(Context, _identity, _tokens)
                .Handle(new BootstrapOwnerCommand("owner@codecamp.demo", "weakpass", "Owner", "0100"), CancellationToken.None));

        Assert.Equal(["Password is too weak."], ex.Errors);
    }
}

using CEMS.Application.Users;
using CEMS.Application.Users.Commands.BootstrapOwner;
using CEMS.Application.Users.Commands.Login;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CEMS.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResultDto>> Login(LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    // Anonymous by necessity: creating the very first account is a chicken-and-egg problem, since
    // every other account-creation path requires an already-authenticated Owner/staff token. This
    // self-disables the moment any user exists (see BootstrapOwnerCommandHandler), so it can't be
    // used as a general signup endpoint.
    [HttpPost("bootstrap-owner")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResultDto>> BootstrapOwner(BootstrapOwnerCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}

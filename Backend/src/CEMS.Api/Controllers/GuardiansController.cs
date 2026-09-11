using CEMS.Application.Students;
using CEMS.Application.Students.Guardians.Commands.CreateGuardian;
using CEMS.Application.Students.Guardians.Commands.UpdateGuardian;
using CEMS.Application.Students.Guardians.Queries.GetGuardianById;
using CEMS.Application.Students.Guardians.Queries.GetGuardians;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CEMS.Api.Controllers;

[ApiController]
[Route("api/guardians")]
public class GuardiansController : ControllerBase
{
    private const string StaffRoles = RoleNames.Owner + "," + RoleNames.BranchManager + "," + RoleNames.FrontDesk;
    private const string ViewRoles = StaffRoles + "," + RoleNames.Parent;

    private readonly IMediator _mediator;

    public GuardiansController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Roles = StaffRoles)]
    public async Task<ActionResult<List<GuardianDto>>> GetGuardians(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetGuardiansQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<GuardianDto>> GetGuardianById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetGuardianByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = StaffRoles)]
    public async Task<ActionResult<GuardianDto>> CreateGuardian(CreateGuardianCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetGuardianById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = StaffRoles)]
    public async Task<ActionResult<GuardianDto>> UpdateGuardian(Guid id, UpdateGuardianRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateGuardianCommand(id, request.FullName, request.Phone, request.Email);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}

public record UpdateGuardianRequest(string FullName, string Phone, string Email);

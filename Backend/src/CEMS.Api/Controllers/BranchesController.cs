using CEMS.Application.Branches;
using CEMS.Application.Branches.Commands.CreateBranch;
using CEMS.Application.Branches.Commands.DeleteBranch;
using CEMS.Application.Branches.Commands.UpdateBranch;
using CEMS.Application.Branches.Queries.GetBranchById;
using CEMS.Application.Branches.Queries.GetBranches;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CEMS.Api.Controllers;

[ApiController]
[Route("api/branches")]
public class BranchesController : ControllerBase
{
    private const string ViewRoles = RoleNames.Owner + "," + RoleNames.BranchManager + "," + RoleNames.FrontDesk;

    private readonly IMediator _mediator;

    public BranchesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<List<BranchDto>>> GetBranches(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetBranchesQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<BranchDto>> GetBranchById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetBranchByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Owner)]
    public async Task<ActionResult<BranchDto>> CreateBranch(CreateBranchCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetBranchById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Owner)]
    public async Task<ActionResult<BranchDto>> UpdateBranch(Guid id, UpdateBranchRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateBranchCommand(id, request.Name, request.Address, request.Phone, request.IsActive);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleNames.Owner)]
    public async Task<IActionResult> DeleteBranch(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteBranchCommand(id), cancellationToken);
        return NoContent();
    }
}

public record UpdateBranchRequest(string Name, string Address, string Phone, bool IsActive);

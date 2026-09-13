using CEMS.Application.Payments;
using CEMS.Application.Payments.Commands.CreatePackage;
using CEMS.Application.Payments.Commands.DeletePackage;
using CEMS.Application.Payments.Commands.UpdatePackage;
using CEMS.Application.Payments.Queries.GetPackageById;
using CEMS.Application.Payments.Queries.GetPackagesForCourse;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CEMS.Api.Controllers;

[ApiController]
public class PackagesController : ControllerBase
{
    private const string ViewRoles = RoleNames.Owner + "," + RoleNames.BranchManager + "," + RoleNames.FrontDesk;
    private const string ManageRoles = RoleNames.Owner + "," + RoleNames.BranchManager;

    private readonly IMediator _mediator;

    public PackagesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("api/courses/{courseId:guid}/packages")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<List<PackageDto>>> GetPackagesForCourse(Guid courseId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPackagesForCourseQuery(courseId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("api/courses/{courseId:guid}/packages")]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<PackageDto>> CreatePackage(Guid courseId, CreatePackageRequest request, CancellationToken cancellationToken)
    {
        var command = new CreatePackageCommand(courseId, request.SessionCount, request.Price);
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetPackageById), new { id = result.Id }, result);
    }

    [HttpGet("api/packages/{id:guid}")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<PackageDto>> GetPackageById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPackageByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPut("api/packages/{id:guid}")]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<PackageDto>> UpdatePackage(Guid id, UpdatePackageRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdatePackageCommand(id, request.SessionCount, request.Price);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("api/packages/{id:guid}")]
    [Authorize(Roles = ManageRoles)]
    public async Task<IActionResult> DeletePackage(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeletePackageCommand(id), cancellationToken);
        return NoContent();
    }
}

public record CreatePackageRequest(int SessionCount, decimal Price);
public record UpdatePackageRequest(int SessionCount, decimal Price);

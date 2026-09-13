using CEMS.Application.Payroll;
using CEMS.Application.Payroll.Commands.ApproveStaffPayrollRun;
using CEMS.Application.Payroll.Commands.MarkStaffPayrollRunPaid;
using CEMS.Application.Payroll.Commands.UpdateStaffPayrollRun;
using CEMS.Application.Payroll.Queries.GenerateStaffPayStub;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CEMS.Api.Controllers;

[ApiController]
[Route("api/staff-payroll-runs")]
public class StaffPayrollRunsController : ControllerBase
{
    private const string ViewRoles = RoleNames.Owner + "," + RoleNames.FrontDesk + "," + RoleNames.BranchManager;

    private readonly IMediator _mediator;

    public StaffPayrollRunsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id:guid}/paystub")]
    [Authorize(Roles = ViewRoles)]
    public async Task<IActionResult> GetPayStub(Guid id, CancellationToken cancellationToken)
    {
        var pdfBytes = await _mediator.Send(new GenerateStaffPayStubQuery(id), cancellationToken);
        return File(pdfBytes, "application/pdf", $"paystub-{id}.pdf");
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Owner)]
    public async Task<ActionResult<StaffPayrollRunDto>> Update(Guid id, UpdateStaffPayrollRunRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateStaffPayrollRunCommand(id, request.Amount), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = RoleNames.Owner)]
    public async Task<ActionResult<StaffPayrollRunDto>> Approve(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ApproveStaffPayrollRunCommand(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/mark-paid")]
    [Authorize(Roles = RoleNames.Owner)]
    public async Task<ActionResult<StaffPayrollRunDto>> MarkPaid(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new MarkStaffPayrollRunPaidCommand(id), cancellationToken);
        return Ok(result);
    }
}

public record UpdateStaffPayrollRunRequest(decimal Amount);

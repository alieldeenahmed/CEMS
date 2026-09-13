using CEMS.Application.Payroll;
using CEMS.Application.Payroll.Commands.ApprovePayrollRun;
using CEMS.Application.Payroll.Commands.MarkPayrollRunPaid;
using CEMS.Application.Payroll.Queries.GetLineItemsForPayrollRun;
using CEMS.Application.Payroll.Queries.GetPayrollRunById;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CEMS.Api.Controllers;

[ApiController]
[Route("api/payroll-runs")]
public class PayrollRunsController : ControllerBase
{
    private const string ViewRoles = RoleNames.Owner + "," + RoleNames.Teacher;

    private readonly IMediator _mediator;

    public PayrollRunsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<PayrollRunDto>> GetPayrollRunById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPayrollRunByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/line-items")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<List<PayrollLineItemDto>>> GetLineItems(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetLineItemsForPayrollRunQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = RoleNames.Owner)]
    public async Task<ActionResult<PayrollRunDto>> Approve(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ApprovePayrollRunCommand(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/mark-paid")]
    [Authorize(Roles = RoleNames.Owner)]
    public async Task<ActionResult<PayrollRunDto>> MarkPaid(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new MarkPayrollRunPaidCommand(id), cancellationToken);
        return Ok(result);
    }
}

using CEMS.Application.Payments;
using CEMS.Application.Payments.Commands.CancelInvoice;
using CEMS.Application.Payments.Commands.RecordPayment;
using CEMS.Application.Payments.Queries.GetInvoiceById;
using CEMS.Application.Payments.Queries.GetPaymentsForInvoice;
using CEMS.Domain.Payments;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CEMS.Api.Controllers;

[ApiController]
[Route("api/invoices")]
public class InvoicesController : ControllerBase
{
    private const string ViewRoles = RoleNames.Owner + "," + RoleNames.BranchManager + "," + RoleNames.FrontDesk;
    private const string PaymentRoles = RoleNames.Owner + "," + RoleNames.BranchManager + "," + RoleNames.FrontDesk;
    private const string CancelRoles = RoleNames.Owner + "," + RoleNames.BranchManager;

    private readonly IMediator _mediator;

    public InvoicesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<InvoiceDto>> GetInvoiceById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetInvoiceByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = CancelRoles)]
    public async Task<ActionResult<InvoiceDto>> CancelInvoice(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CancelInvoiceCommand(id), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/payments")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<List<PaymentDto>>> GetPayments(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPaymentsForInvoiceQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/payments")]
    [Authorize(Roles = PaymentRoles)]
    public async Task<ActionResult<PaymentDto>> RecordPayment(Guid id, RecordPaymentRequest request, CancellationToken cancellationToken)
    {
        var command = new RecordPaymentCommand(id, request.AmountPaid, request.PaymentDate, request.Method);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}

public record RecordPaymentRequest(decimal AmountPaid, DateOnly PaymentDate, PaymentMethod Method);

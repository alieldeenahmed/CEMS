using CEMS.Application.Analytics;
using CEMS.Application.Analytics.Queries.ExportDashboard;
using CEMS.Application.Analytics.Queries.GetAttendanceTrends;
using CEMS.Application.Analytics.Queries.GetDashboardSummary;
using CEMS.Application.Analytics.Queries.GetEnrollmentFunnel;
using CEMS.Application.Analytics.Queries.GetRevenueSummary;
using CEMS.Application.Analytics.Queries.GetTeacherUtilization;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CEMS.Api.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize(Roles = RoleNames.Owner + "," + RoleNames.BranchManager)]
public class AnalyticsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AnalyticsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("revenue")]
    public async Task<ActionResult<RevenueSummaryDto>> GetRevenueSummary(
        [FromQuery] Guid? branchId, [FromQuery] DateOnly periodStart, [FromQuery] DateOnly periodEnd, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetRevenueSummaryQuery(branchId, periodStart, periodEnd), cancellationToken);
        return Ok(result);
    }

    [HttpGet("teacher-utilization")]
    public async Task<ActionResult<List<TeacherUtilizationDto>>> GetTeacherUtilization(
        [FromQuery] Guid? branchId, [FromQuery] DateOnly periodStart, [FromQuery] DateOnly periodEnd, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetTeacherUtilizationQuery(branchId, periodStart, periodEnd), cancellationToken);
        return Ok(result);
    }

    [HttpGet("attendance-trends")]
    public async Task<ActionResult<AttendanceTrendsDto>> GetAttendanceTrends(
        [FromQuery] Guid? branchId, [FromQuery] DateOnly periodStart, [FromQuery] DateOnly periodEnd, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAttendanceTrendsQuery(branchId, periodStart, periodEnd), cancellationToken);
        return Ok(result);
    }

    [HttpGet("enrollment-funnel")]
    public async Task<ActionResult<EnrollmentFunnelDto>> GetEnrollmentFunnel([FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetEnrollmentFunnelQuery(branchId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardSummaryDto>> GetDashboard(
        [FromQuery] Guid? branchId, [FromQuery] DateOnly periodStart, [FromQuery] DateOnly periodEnd, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetDashboardSummaryQuery(branchId, periodStart, periodEnd), cancellationToken);
        return Ok(result);
    }

    [HttpGet("dashboard/export/pdf")]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] Guid? branchId, [FromQuery] DateOnly periodStart, [FromQuery] DateOnly periodEnd, CancellationToken cancellationToken)
    {
        var pdfBytes = await _mediator.Send(new ExportDashboardPdfQuery(branchId, periodStart, periodEnd), cancellationToken);
        return File(pdfBytes, "application/pdf", "dashboard.pdf");
    }

    [HttpGet("dashboard/export/excel")]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] Guid? branchId, [FromQuery] DateOnly periodStart, [FromQuery] DateOnly periodEnd, CancellationToken cancellationToken)
    {
        var excelBytes = await _mediator.Send(new ExportDashboardExcelQuery(branchId, periodStart, periodEnd), cancellationToken);
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "dashboard.xlsx");
    }
}

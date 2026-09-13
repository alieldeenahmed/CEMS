using MediatR;

namespace CEMS.Application.Analytics.Queries.ExportDashboard;

public record ExportDashboardPdfQuery(Guid? BranchId, DateOnly PeriodStart, DateOnly PeriodEnd) : IRequest<byte[]>;

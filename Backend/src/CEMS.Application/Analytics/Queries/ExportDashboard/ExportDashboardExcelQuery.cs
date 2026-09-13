using MediatR;

namespace CEMS.Application.Analytics.Queries.ExportDashboard;

public record ExportDashboardExcelQuery(Guid? BranchId, DateOnly PeriodStart, DateOnly PeriodEnd) : IRequest<byte[]>;

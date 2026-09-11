using MediatR;

namespace CEMS.Application.Exams.Queries.GenerateReportCard;

public record GenerateReportCardQuery(Guid StudentId) : IRequest<byte[]>;

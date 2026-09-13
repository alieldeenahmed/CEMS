namespace CEMS.Application.Payments;

public record OutstandingBalanceDto(Guid StudentId, decimal TotalOutstanding);

namespace CEMS.Application.Students;

public record StudentBranchHistoryDto(
    Guid Id,
    Guid StudentId,
    Guid FromBranchId,
    string FromBranchName,
    Guid ToBranchId,
    string ToBranchName,
    DateOnly TransferDate,
    string? Reason,
    Guid? TransferredByUserId);

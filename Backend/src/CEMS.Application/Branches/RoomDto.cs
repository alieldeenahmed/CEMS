namespace CEMS.Application.Branches;

public record RoomDto(Guid Id, Guid BranchId, string Name, int Capacity);

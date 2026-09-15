using MediatR;

namespace CEMS.Application.Users.Queries.GetBranchStaff;

public record GetBranchStaffQuery : IRequest<List<StaffUserDto>>;

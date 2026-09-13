using MediatR;

namespace CEMS.Application.Users.Queries.GetStaffUsers;

public record GetStaffUsersQuery : IRequest<List<StaffUserDto>>;

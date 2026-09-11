using MediatR;

namespace CEMS.Application.Users.Commands.CreateStaffUser;

public record CreateStaffUserCommand(string Email, string Password, string FullName, string PhoneNumber, string Role, Guid? BranchId)
    : IRequest<StaffUserDto>;

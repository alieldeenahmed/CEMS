using CEMS.Domain.Branches;
using CEMS.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Branch> Branches { get; }
    DbSet<Room> Rooms { get; }
    DbSet<UserBranchAssignment> UserBranchAssignments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

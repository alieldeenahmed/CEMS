using CEMS.Domain.Branches;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Branch> Branches { get; }
    DbSet<Room> Rooms { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

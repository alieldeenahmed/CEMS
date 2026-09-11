using CEMS.Domain.Branches;
using CEMS.Domain.Users;
using CEMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<UserBranchAssignment> UserBranchAssignments => Set<UserBranchAssignment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        builder.Entity<IdentityRole<Guid>>().HasData(
            new IdentityRole<Guid>
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = RoleNames.Owner,
                NormalizedName = RoleNames.Owner.ToUpperInvariant(),
                ConcurrencyStamp = "11111111-1111-1111-1111-111111111111"
            },
            new IdentityRole<Guid>
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = RoleNames.BranchManager,
                NormalizedName = RoleNames.BranchManager.ToUpperInvariant(),
                ConcurrencyStamp = "22222222-2222-2222-2222-222222222222"
            },
            new IdentityRole<Guid>
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = RoleNames.Teacher,
                NormalizedName = RoleNames.Teacher.ToUpperInvariant(),
                ConcurrencyStamp = "33333333-3333-3333-3333-333333333333"
            },
            new IdentityRole<Guid>
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Name = RoleNames.FrontDesk,
                NormalizedName = RoleNames.FrontDesk.ToUpperInvariant(),
                ConcurrencyStamp = "44444444-4444-4444-4444-444444444444"
            },
            new IdentityRole<Guid>
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Name = RoleNames.Parent,
                NormalizedName = RoleNames.Parent.ToUpperInvariant(),
                ConcurrencyStamp = "55555555-5555-5555-5555-555555555555"
            }
        );
    }
}

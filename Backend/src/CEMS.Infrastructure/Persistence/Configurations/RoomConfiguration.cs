using CEMS.Domain.Branches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEMS.Infrastructure.Persistence.Configurations;

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.Property(r => r.Name).IsRequired().HasMaxLength(100);

        builder.HasOne(r => r.Branch)
            .WithMany(b => b.Rooms)
            .HasForeignKey(r => r.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

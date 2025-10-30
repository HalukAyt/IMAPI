using IMAPI.Api.Entities;
using Microsoft.EntityFrameworkCore;


namespace IMAPI.Api.Data;


public class ItechMarineDbContext : DbContext
{
    public ItechMarineDbContext(DbContextOptions<ItechMarineDbContext> options) : base(options) { }


    public DbSet<User> Users => Set<User>();
    public DbSet<Boat> Boats => Set<Boat>();
    public DbSet<Device> Devices => Set<Device>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
        });


        modelBuilder.Entity<Boat>(e =>
        {
            e.HasOne(b => b.Owner)
            .WithMany(u => u.Boats)
            .HasForeignKey(b => b.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
        });


        modelBuilder.Entity<Device>(e =>
        {
            e.HasOne(d => d.Boat)
            .WithMany(b => b.Devices)
            .HasForeignKey(d => d.BoatId)
            .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Serial).IsUnique();
        });
    }
}
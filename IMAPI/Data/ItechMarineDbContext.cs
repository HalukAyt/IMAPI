using IMAPI.Api.Entities;
using Microsoft.EntityFrameworkCore;


namespace IMAPI.Api.Data;


public class ItechMarineDbContext : DbContext
{
    public ItechMarineDbContext(DbContextOptions<ItechMarineDbContext> options) : base(options) { }


    public DbSet<User> Users => Set<User>();
    public DbSet<Boat> Boats => Set<Boat>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<LightChannel> LightChannels => Set<LightChannel>();
    public DbSet<Telemetry> Telemetries => Set<Telemetry>();
    public DbSet<PendingCommand> PendingCommands => Set<PendingCommand>();





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

        modelBuilder.Entity<LightChannel>(e =>
        {
            e.HasIndex(x => new { x.DeviceId, x.ChNo }).IsUnique();
            e.HasOne(x => x.Device)
             .WithMany(d => d.LightChannels)
             .HasForeignKey(x => x.DeviceId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Telemetry>(e =>
        {
            e.HasIndex(x => x.DeviceSerial);
            e.HasIndex(x => x.Ts);
        });

        modelBuilder.Entity<PendingCommand>(e =>
        {
            e.HasIndex(x => new { x.DeviceSerial, x.Status });
            e.HasIndex(x => x.ExpiresAt);
        });


    }
}
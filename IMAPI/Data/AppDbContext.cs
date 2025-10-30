namespace IMAPI.Data;

using IMAPI.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

public class AppDbContext : IdentityDbContext<AppUser>, IDataProtectionKeyContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Boat> Boats => Set<Boat>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<RelayChannel> RelayChannels => Set<RelayChannel>();
    public DbSet<Telemetry> Telemetries => Set<Telemetry>();
    public DbSet<Command> Commands => Set<Command>();

    // DataProtection keys table
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Device>().HasIndex(x => x.DeviceId).IsUnique();
        b.Entity<Boat>().HasOne(x => x.Owner).WithMany(u => u.Boats).HasForeignKey(x => x.OwnerId);
        b.Entity<RelayChannel>().HasIndex(x => new { x.DeviceId, x.Index }).IsUnique();
    }
}
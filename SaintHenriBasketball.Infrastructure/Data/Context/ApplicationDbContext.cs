using SaintHenriBasketball.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace SaintHenriBasketball.Infrastructure.Data.Context;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Player> Players { get; set; }
    public DbSet<TrainingSession> TrainingSessions { get; set; }
    public DbSet<SessionRegistration> SessionRegistrations { get; set; }
    public DbSet<Payment> Payments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Player>()
            .HasMany(p => p.SessionRegistrations)
            .WithOne(sr => sr.Player)
            .HasForeignKey(sr => sr.PlayerId);

        modelBuilder.Entity<TrainingSession>()
            .HasMany(s => s.Registrations)
            .WithOne(sr => sr.Session)
            .HasForeignKey(sr => sr.SessionId);
    }
}
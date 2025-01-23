using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.TestUtils.Builders;
using System.Data.Common;

namespace SaintHenriBasketball.TestUtils.Fixtures;

public class TestDbFixture : IDisposable
{
    private const string ConnectionString = @"Server=(localdb)\mssqllocaldb;Database={0};Trusted_Connection=True;MultipleActiveResultSets=true";
    private readonly string _databaseName;
    private readonly ApplicationDbContext _context;

    public TestDbFixture()
    {
        _databaseName = $"TestDb_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(string.Format(ConnectionString, _databaseName))
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        SeedTestData();
    }

    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(string.Format(ConnectionString, _databaseName))
            .Options;

        return new ApplicationDbContext(options);
    }

    private void SeedTestData()
    {
        // Create test users
        var adminUser = new ApplicationUserBuilder()
            .WithUsername("admin")
            .WithEmail("admin@example.com")
            .WithAdminRole()
            .WithPaymentPlan(PaymentPlan.Season)
            .WithEmailConfirmed()
            .Build();

        var regularUser = new ApplicationUserBuilder()
            .WithUsername("user")
            .WithEmail("user@example.com")
            .WithPaymentPlan(PaymentPlan.DropIn)
            .WithEmailConfirmed()
            .Build();

        _context.Users.AddRange(adminUser, regularUser);
        _context.SaveChanges();

        // Create test seasons
        var currentSeason = new SeasonBuilder()
            .WithDates(DateTime.UtcNow, DateTime.UtcNow.AddMonths(3))
            .WithPrice(299.99m)
            .WithStatus(SeasonStatus.Open)
            .Build();

        var pastSeason = new SeasonBuilder()
            .WithDates(DateTime.UtcNow.AddMonths(-6), DateTime.UtcNow.AddMonths(-3))
            .WithPrice(279.99m)
            .WithStatus(SeasonStatus.Closed)
            .Build();

        _context.Seasons.AddRange(currentSeason, pastSeason);
        _context.SaveChanges();

        // Create test sessions
        var upcomingSession = new SessionBuilder()
            .WithDate(DateTime.UtcNow.AddDays(1))
            .WithCapacity(20)
            .WithStatus(SessionStatus.Open)
            .Build();

        var pastSession = new SessionBuilder()
            .WithDate(DateTime.UtcNow.AddDays(-1))
            .WithCapacity(20)
            .WithStatus(SessionStatus.Completed)
            .Build();

        _context.Sessions.AddRange(upcomingSession, pastSession);
        _context.SaveChanges();

        // Create test payments
        var payment = new PaymentBuilder()
            .ForUser(regularUser.Id)
            .WithAmount(299.99m)
            .WithStatus(PaymentStatus.Completed)
            .Build();

        _context.Payments.Add(payment);

        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    // Helper methods to get test data IDs
    public async Task<Guid> GetTestAdminUserId()
    {
        return await _context.Users
            .Where(u => u.Email == "admin@example.com")
            .Select(u => u.Id)
            .FirstAsync();
    }

    public async Task<Guid> GetTestRegularUserId()
    {
        return await _context.Users
            .Where(u => u.Email == "user@example.com")
            .Select(u => u.Id)
            .FirstAsync();
    }

    public async Task<Guid> GetCurrentSeasonId()
    {
        return await _context.Seasons
            .Where(s => s.Status == SeasonStatus.Open)
            .Select(s => s.Id)
            .FirstAsync();
    }

    public async Task<Guid> GetUpcomingSessionId()
    {
        return await _context.Sessions
            .Where(s => s.SessionDate > DateTime.UtcNow)
            .Select(s => s.Id)
            .FirstAsync();
    }
}
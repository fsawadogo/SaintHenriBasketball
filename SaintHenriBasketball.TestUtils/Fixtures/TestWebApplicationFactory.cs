using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.TestUtils.Fixtures;

public class TestWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    private readonly string _databaseName;

    public TestWebApplicationFactory()
    {
        _databaseName = $"TestDb_{Guid.NewGuid()}";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add SQL Server test database
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlServer(
                    $"Server=(localdb)\\mssqllocaldb;Database=SaintHenriDB;Trusted_Connection=True;MultipleActiveResultSets=true"
                );
            });

            // Create a new service provider
            var serviceProvider = services.BuildServiceProvider();

            // Create a scope to obtain a reference to the database context
            using var scope = serviceProvider.CreateScope();
            var scopedServices = scope.ServiceProvider;
            var db = scopedServices.GetRequiredService<ApplicationDbContext>();
            var logger = scopedServices.GetRequiredService<ILogger<TestWebApplicationFactory<TProgram>>>();

            // Ensure the database is created
            db.Database.EnsureCreated();

            try
            {
                InitializeDatabase(db);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred seeding the database. Error: {Message}", ex.Message);
            }
        });
    }

    private void InitializeDatabase(ApplicationDbContext context)
    {
        // Add test data
        context.Users.AddRange(GetTestUsers());
        context.SaveChanges();
    }

    private IEnumerable<ApplicationUser> GetTestUsers()
    {
        var adminUser = new ApplicationUser(
            username: "testuser",
            email: "test@example.com",
            passwordHash: "AQAAAAIAAYagAAAAELbKo2uE+7OG4LvXVoX/0V3jaSxTZGhLLgnS9vVZvbINSvjlOoJ7KbHfxp7mwEX2ww==", // Hash for "Password123!"
            firstName: "Test",
            lastName: "Admin",
            paymentPlan: PaymentPlan.Season
        )
        {
            IsAdmin = true,
            EmailConfirmed = true
        };

        var regularUser = new ApplicationUser(
            username: "regularuser",
            email: "regular@example.com",
            passwordHash: "AQAAAAIAAYagAAAAELbKo2uE+7OG4LvXVoX/0V3jaSxTZGhLLgnS9vVZvbINSvjlOoJ7KbHfxp7mwEX2ww==", // Hash for "Password123!"
            firstName: "Regular",
            lastName: "User",
            paymentPlan: PaymentPlan.DropIn
        )
        {
            IsAdmin = false,
            EmailConfirmed = true
        };

        return new List<ApplicationUser> { adminUser, regularUser };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Delete the test database
            using var scope = Services.CreateScope();
            var scopedServices = scope.ServiceProvider;
            var db = scopedServices.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureDeleted();
        }

        base.Dispose(disposing);
    }
}
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Repositories;
using SaintHenriBasketball.TestUtils.Fixtures;

namespace SaintHenriBasketball.IntegrationTests.Repositories;

public class UserRepositoryTests : IClassFixture<TestDbFixture>
{
    private readonly TestDbFixture _fixture;
    private readonly ILogger<UserRepository> _logger;

    public UserRepositoryTests(TestDbFixture fixture)
    {
        _fixture = fixture;
        _logger = new NullLogger<UserRepository>();
    }

    [Fact]
    public async Task GetUserByEmail_ReturnsCorrectUser()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var repository = new UserRepository(context, _logger);

        // Act
        var user = await repository.GetByEmailAsync("test@example.com");

        // Assert
        Assert.NotNull(user);
        Assert.Equal("testuser", user.Username);
        Assert.True(user.IsAdmin);
    }

    [Fact]
    public async Task CreateUser_SavesUserToDatabase()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var repository = new UserRepository(context, _logger);

        var newUser = new ApplicationUser(
            username: "newuser",
            email: "new@example.com",
            passwordHash: "hashedpassword",
            firstName: "New",
            lastName: "User",
            paymentPlan: PaymentPlan.DropIn
        );

        // Act
        await repository.AddAsync(newUser);

        // Assert
        var savedUser = await context.Users.FindAsync(newUser.Id);
        Assert.NotNull(savedUser);
        Assert.Equal("new@example.com", savedUser.Email);
    }

    [Fact]
    public async Task UpdateUser_UpdatesUserInDatabase()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var repository = new UserRepository(context, _logger);

        var user = await repository.GetByEmailAsync("regular@example.com");
        var originalUsername = user.Username;

        // Act
        user.Username = "updatedusername";
        await repository.UpdateAsync(user);

        // Assert
        await using var newContext = _fixture.CreateContext();
        var updatedUser = await newContext.Users.FindAsync(user.Id);
        Assert.NotEqual(originalUsername, updatedUser.Username);
        Assert.Equal("updatedusername", updatedUser.Username);
    }

    [Fact]
    public async Task DeleteUser_RemovesUserFromDatabase()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var repository = new UserRepository(context, _logger);

        var user = await repository.GetByEmailAsync("regular@example.com");

        // Act
        await repository.DeleteAsync(user);

        // Assert
        await using var newContext = _fixture.CreateContext();
        var deletedUser = await newContext.Users.FindAsync(user.Id);
        Assert.Null(deletedUser);
    }
}

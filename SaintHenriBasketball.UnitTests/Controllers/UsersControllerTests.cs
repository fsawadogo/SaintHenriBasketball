using Microsoft.VisualStudio.TestPlatform.TestHost;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.TestUtils.Fixtures;
using System.Net.Http.Json;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.IntegrationTests.Helpers;

namespace SaintHenriBasketball.UnitTests.Controllers;

public class UsersControllerTests : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly TestWebApplicationFactory<Program> _factory;
    private readonly TestAuthHandler _authHandler;

    public UsersControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _authHandler = new TestAuthHandler(factory);
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsCreated()
    {
        // Arrange
        var client = _factory.CreateClient();
        var registerDto = new RegisterUserDto
        {
            Username = "newuser",
            Email = "newuser@example.com",
            Password = "NewPassword123!",
            FirstName = "New",
            LastName = "User",
            PaymentPlan = PaymentPlan.DropIn
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/users/register", registerDto);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UserResponseDto>();
        Assert.Equal(registerDto.Email, result.Email);
        Assert.Equal(registerDto.Username, result.Username);
    }

    [Fact]
    public async Task GetAllUsers_AsAdmin_ReturnsAllUsers()
    {
        // Arrange
        var token = await _authHandler.GetJwtToken("testuser", "Password123!");
        var client = _authHandler.CreateAuthorizedClient(token);

        // Act
        var response = await client.GetAsync("/api/users");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<List<UserDto>>();
        Assert.NotNull(users);
        Assert.Contains(users, u => u.Email == "test@example.com");
        Assert.Contains(users, u => u.Email == "regular@example.com");
    }

    [Fact]
    public async Task GetAllUsers_AsRegularUser_ReturnsForbidden()
    {
        // Arrange
        var token = await _authHandler.GetJwtToken("regularuser", "Password123!");
        var client = _authHandler.CreateAuthorizedClient(token);

        // Act
        var response = await client.GetAsync("/api/users");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCurrentUser_WithValidData_UpdatesProfile()
    {
        // Arrange
        var token = await _authHandler.GetJwtToken("regularuser", "Password123!");
        var client = _authHandler.CreateAuthorizedClient(token);
        var updateDto = new UpdateUserDto
        {
            Username = "regularuser",
            FirstName = "Updated",
            LastName = "Name",
            Email = "updated@example.com"
        };

        // Act
        var response = await client.PutAsJsonAsync("/api/users/me", updateDto);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify the update
        var userResponse = await client.GetAsync("/api/users/me");
        var updatedUser = await userResponse.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(updateDto.FirstName, updatedUser.FirstName);
        Assert.Equal(updateDto.LastName, updatedUser.LastName);
        Assert.Equal(updateDto.Email, updatedUser.Email);
    }

    [Fact]
    public async Task UpdatePaymentPlan_AsAdmin_UpdatesPlan()
    {
        // Arrange
        var token = await _authHandler.GetJwtToken("testuser", "Password123!");
        var client = _authHandler.CreateAuthorizedClient(token);
        var updateDto = new UpdatePaymentPlanDto
        {
            PaymentPlan = PaymentPlan.Season
        };

        // Act
        var response = await client.PatchAsync(
            "/api/users/update-payment-plan?email=regular@example.com",
            JsonContent.Create(updateDto));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify the update in database
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updatedUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == "regular@example.com");
        Assert.Equal(PaymentPlan.Season, updatedUser.PaymentPlan);
    }

    [Fact]
    public async Task MakeUserAdmin_AsAdmin_UpdatesAdminStatus()
    {
        // Arrange
        var token = await _authHandler.GetJwtToken("testuser", "Password123!");
        var client = _authHandler.CreateAuthorizedClient(token);

        // Get the regular user's ID first
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var regularUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == "regular@example.com");
        var regularUserId = regularUser.Id;

        // Act
        var response = await client.PatchAsync($"/api/users/{regularUserId}/make-admin", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify the update in database
        await dbContext.Entry(regularUser).ReloadAsync();
        Assert.True(regularUser.IsAdmin);
    }

    [Fact]
    public async Task DeleteUser_AsAdmin_DeletesUser()
    {
        // Arrange
        var token = await _authHandler.GetJwtToken("testuser", "Password123!");
        var client = _authHandler.CreateAuthorizedClient(token);

        // Create a user to delete
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userToDelete = new ApplicationUser(
            "deleteuser",
            "delete@example.com",
            "hashedpassword",
            "Delete",
            "User",
            PaymentPlan.DropIn);
        dbContext.Users.Add(userToDelete);
        await dbContext.SaveChangesAsync();

        // Act
        var response = await client.DeleteAsync($"/api/users/{userToDelete.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify the user is deleted
        var deletedUser = await dbContext.Users.FindAsync(userToDelete.Id);
        Assert.Null(deletedUser);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        // Arrange
        var client = _factory.CreateClient();
        var loginDto = new LoginDto
        {
            UserName = "testuser",
            Password = "Password123!"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/users/login", loginDto);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UserResponseDto>();
        Assert.NotNull(result.Token);
        Assert.Equal(loginDto.UserName, result.Username);
    }

    [Fact]
    public async Task GetCurrentUser_WithValidToken_ReturnsUserInfo()
    {
        // Arrange
        var token = await _authHandler.GetJwtToken("regularuser", "Password123!");
        var client = _authHandler.CreateAuthorizedClient(token);

        // Act
        var response = await client.GetAsync("/api/users/me");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal("regularuser", user.Username);
        Assert.Equal("regular@example.com", user.Email);
        Assert.False(user.IsAdmin);
    }
}

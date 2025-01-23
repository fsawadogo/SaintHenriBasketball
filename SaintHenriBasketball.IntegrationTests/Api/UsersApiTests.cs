using Microsoft.VisualStudio.TestPlatform.TestHost;
using SaintHenriBasketball.Application.DTOs.Users;
using System.Net.Http.Json;
using System.Net;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.TestUtils.Fixtures;
using SaintHenriBasketball.IntegrationTests.Helpers;

namespace SaintHenriBasketball.IntegrationTests.Api;

public class UsersApiTests : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly TestWebApplicationFactory<Program> _factory;
    private readonly TestAuthHandler _authHandler;

    public UsersApiTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _authHandler = new TestAuthHandler(factory);
    }

    [Fact]
    public async Task GetUsers_AsAdmin_ReturnsAllUsers()
    {
        // Arrange
        var token = await _authHandler.GetJwtToken("testuser", "YourTestPassword123!");
        var client = _authHandler.CreateAuthorizedClient(token);

        // Act
        var response = await client.GetAsync("/api/users");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<List<UserDto>>();
        Assert.NotEmpty(users);
    }

    [Fact]
    public async Task GetUsers_AsRegularUser_ReturnsForbidden()
    {
        // Arrange
        var token = await _authHandler.GetJwtToken("regularuser", "YourTestPassword123!");
        var client = _authHandler.CreateAuthorizedClient(token);

        // Act
        var response = await client.GetAsync("/api/users");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
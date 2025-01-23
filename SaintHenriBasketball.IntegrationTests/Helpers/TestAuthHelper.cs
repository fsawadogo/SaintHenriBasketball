using Microsoft.VisualStudio.TestPlatform.TestHost;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.TestUtils.Fixtures;
using System.Net.Http.Json;

namespace SaintHenriBasketball.IntegrationTests.Helpers;

public class TestAuthHandler
{
    private readonly TestWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public TestAuthHandler(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task<string> GetJwtToken(string username, string password)
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new
        {
            UserName = username,
            Password = password
        });

        if (!loginResponse.IsSuccessStatusCode)
            throw new Exception("Authentication failed in test");

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<UserResponseDto>();
        return loginResult.Token;
    }

    public HttpClient CreateAuthorizedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

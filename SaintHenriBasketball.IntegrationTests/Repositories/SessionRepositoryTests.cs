using SaintHenriBasketball.Infrastructure.Data.Repositories;
using SaintHenriBasketball.TestUtils.Fixtures;

namespace SaintHenriBasketball.IntegrationTests.Repositories;

public class SessionRepositoryTests : IClassFixture<TestDbFixture>
{
    private readonly TestDbFixture _fixture;

    public SessionRepositoryTests(TestDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetUpcomingUserSessions_ReturnsCorrectSessions()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var repository = new SessionRepository(context);
        var userId = await _fixture.GetTestRegularUserId();

        // Act
        var sessions = await repository.GetUserSessionsAsync(userId);

        // Assert
        Assert.NotEmpty(sessions);
        Assert.All(sessions, s => Assert.True(s.SessionDate > DateTime.UtcNow));
    }
}

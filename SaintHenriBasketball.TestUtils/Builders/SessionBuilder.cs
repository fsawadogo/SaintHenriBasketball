using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.TestUtils.Builders;

public class SessionBuilder
{
    private readonly Session _session;

    public SessionBuilder()
    {
        _session = new Session(
            sessionDate: DateTime.UtcNow.AddDays(1),
            maxCapacity: 20,
            dropInPrice: 15.00m,
            startTime: "18:00",
            endTime: "20:00",
            location: "Saint Henri YMCA"
        );
    }

    public SessionBuilder WithDate(DateTime sessionDate)
    {
        _session.SessionDate = sessionDate;
        return this;
    }

    public SessionBuilder WithCapacity(int maxCapacity)
    {
        _session.MaxCapacity = maxCapacity;
        return this;
    }

    public SessionBuilder WithPrice(decimal dropInPrice)
    {
        _session.DropInPrice = dropInPrice;
        return this;
    }

    public SessionBuilder WithTime(string startTime, string endTime)
    {
        _session.StartTime = startTime;
        _session.EndTime = endTime;
        return this;
    }

    public SessionBuilder WithLocation(string location)
    {
        _session.Location = location;
        return this;
    }

    public SessionBuilder WithStatus(SessionStatus status)
    {
        _session.Status = status;
        return this;
    }

    public SessionBuilder WithRegisteredPlayersCount(int count)
    {
        _session.RegisteredPlayersCount = count;
        return this;
    }

    public Session Build() => _session;
}

using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.TestUtils.Builders;

public class SeasonBuilder
{
    private readonly Season _season;

    public SeasonBuilder()
    {
        _season = new Season(
            startDate: DateTime.UtcNow,
            endDate: DateTime.UtcNow.AddMonths(3),
            price: 299.99m,
            notes: "Test Season"
        );
    }

    public SeasonBuilder WithDates(DateTime startDate, DateTime endDate)
    {
        _season.StartDate = startDate;
        _season.EndDate = endDate;
        return this;
    }

    public SeasonBuilder WithPrice(decimal price)
    {
        _season.Price = price;
        return this;
    }

    public SeasonBuilder WithStatus(SeasonStatus status)
    {
        _season.Status = status;
        return this;
    }

    public SeasonBuilder WithNotes(string notes)
    {
        _season.Notes = notes;
        return this;
    }

    public Season Build() => _season;
}

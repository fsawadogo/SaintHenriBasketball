using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.SeasonSchedule;

/// One session day of a season: 0 = Sunday … 6 = Saturday, with its own time, spots, price and gym.
public class SeasonScheduleDayDto
{
    public int DayOfWeek { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public int MaxCapacity { get; set; } = 20;
    public decimal DropInPrice { get; set; } = 10m;
    public string Location { get; set; } = string.Empty;
}

public class SeasonSchedulePreviewRequestDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<SeasonScheduleDayDto> Days { get; set; } = new();
}

public class SeasonSchedulePreviewSessionDto
{
    public DateTime Date { get; set; }
    public int DayOfWeek { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public int MaxCapacity { get; set; }
    public decimal DropInPrice { get; set; }
    public string Location { get; set; } = string.Empty;
    public bool AlreadyExists { get; set; }
}

public class SeasonSchedulePreviewDto
{
    public List<SeasonSchedulePreviewSessionDto> Sessions { get; set; } = new();
    public int SessionsToCreate { get; set; }
    public int SessionsAlreadyExisting { get; set; }
    /// True when another season is open, so this one would be created closed.
    public bool WillBeClosed { get; set; }
    public string? OpenSeasonName { get; set; }
}

/// A date the admin unticked in the preview.
public class SeasonScheduleSkipDto
{
    public DateTime Date { get; set; }
    public string StartTime { get; set; } = string.Empty;
}

public class CreateSeasonWithScheduleDto
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Price { get; set; }
    public string? Notes { get; set; }
    /// How many players may hold a season pass. The wizard is the main way seasons are created, so
    /// capacity has to be settable here and not only on the plain create endpoint.
    public int SeasonPassCapacity { get; set; } = Domain.Entities.Season.DefaultSeasonPassCapacity;
    public List<SeasonScheduleDayDto> Days { get; set; } = new();
    public List<SeasonScheduleSkipDto> Skip { get; set; } = new();
}

public class SeasonScheduleCreateResultDto
{
    public Guid SeasonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public SeasonStatus Status { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int SessionsCreated { get; set; }
    public int SessionsSkipped { get; set; }
    public int SessionsAlreadyExisting { get; set; }
}

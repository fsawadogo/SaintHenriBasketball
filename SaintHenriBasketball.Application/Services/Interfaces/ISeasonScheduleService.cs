using SaintHenriBasketball.Application.DTOs.SeasonSchedule;

namespace SaintHenriBasketball.Application.Services.Interfaces;

/// Creates a season together with every session of its schedule (feature flag `season-schedule-wizard`).
public interface ISeasonScheduleService
{
    /// The sessions the wizard would create, and which of them already exist. Writes nothing.
    Task<SeasonSchedulePreviewDto> PreviewAsync(SeasonSchedulePreviewRequestDto request);

    /// Creates the season and its sessions in one save.
    Task<SeasonScheduleCreateResultDto> CreateAsync(CreateSeasonWithScheduleDto request, Guid? adminId, string adminName);
}

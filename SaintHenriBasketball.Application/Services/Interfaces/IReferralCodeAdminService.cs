using SaintHenriBasketball.Application.DTOs.PromoReferralReports;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IReferralCodeAdminService
{
    Task<ReferralCodeAdminPageDto> SearchAsync(ReferralCodeAdminQuery query);

    /// <summary>
    /// Sets IsActive and MaxUses and audits the before and after values. Throws ValidationException when
    /// MaxUses is below 1, or below the current uses without AllowBelowCurrentUses; NotFoundException for an unknown code.
    /// </summary>
    Task<ReferralCodeAdminDto> UpdateAsync(Guid id, UpdateReferralCodeDto body, Guid? adminId, string adminName);
}

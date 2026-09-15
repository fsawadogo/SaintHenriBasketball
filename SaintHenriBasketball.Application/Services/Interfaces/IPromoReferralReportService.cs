using SaintHenriBasketball.Application.DTOs.PromoReferralReports;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IPromoReferralReportService
{
    /// One row per promo code with its completed uses in the range. Throws ValidationException for a reversed range.
    Task<PromoUsageReportDto> GetPromoUsageAsync(ReportRangeQuery range);

    /// Credits granted, spent, refunded and adjusted in the range, and the outstanding balance at its end.
    Task<CreditsReportDto> GetCreditsReportAsync(ReportRangeQuery range);
}

using SaintHenriBasketball.Application.DTOs.TreasurerReport;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface ITreasurerReportService
{
    /// Collected, outstanding, promo, credit and refund totals by month, season and overall.
    /// Throws ValidationException for a missing, reversed or longer-than-3-year range, and NotFoundException for an unknown season.
    Task<TreasurerReportDto> GetReportAsync(TreasurerReportQuery query);

    /// The same report as a UTF-8 (with BOM) CSV with a payment-level detail section. Writes an "Exported" audit entry
    /// before returning; if the audit entry can't be written, the export fails.
    Task<TreasurerReportExport> ExportCsvAsync(TreasurerReportQuery query, Guid? adminId, string adminName);
}

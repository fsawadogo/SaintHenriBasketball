using SaintHenriBasketball.Application.DTOs.TaxReceipts;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface ITaxReceiptService
{
    Task<IReadOnlyList<TaxReceiptYearDto>> GetAvailableYearsAsync(Guid userId);
    Task<(byte[] Pdf, string FileName)> GenerateAsync(Guid userId, int year);
}

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.TaxReceipts;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class TaxReceiptService : ITaxReceiptService
{
    private readonly IUserRepository _userRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly ILogger<TaxReceiptService> _logger;

    public TaxReceiptService(
        IUserRepository userRepository,
        IPaymentRepository paymentRepository,
        IWebHostEnvironment webHostEnvironment,
        ILogger<TaxReceiptService> logger)
    {
        _userRepository = userRepository;
        _paymentRepository = paymentRepository;
        _webHostEnvironment = webHostEnvironment;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TaxReceiptYearDto>> GetAvailableYearsAsync(Guid userId)
    {
        var payments = await _paymentRepository.GetPaymentsByUserAsync(userId);
        return payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .GroupBy(p => p.PaymentDate.Year)
            .OrderByDescending(g => g.Key)
            .Select(g => new TaxReceiptYearDto
            {
                Year = g.Key,
                PaymentCount = g.Count(),
                TotalAmount = g.Sum(p => p.Amount),
            })
            .ToList();
    }

    public async Task<(byte[] Pdf, string FileName)> GenerateAsync(Guid userId, int year)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException($"User {userId} not found");

        var payments = await _paymentRepository.GetPaymentsByUserAsync(userId);
        var yearPayments = payments
            .Where(p => p.Status == PaymentStatus.Completed && p.PaymentDate.Year == year)
            .OrderBy(p => p.PaymentDate)
            .ToList();

        if (yearPayments.Count == 0)
            throw new NotFoundException($"No completed payments found for {year}");

        var lang = user.PreferredLanguage == EmailLanguage.French ? "fr" : "en";

        var receipt = new TaxReceiptDto
        {
            Year = year,
            UserName = $"{user.FirstName} {user.LastName}".Trim(),
            UserEmail = user.Email,
            TotalAmount = yearPayments.Sum(p => p.Amount),
            Lines = yearPayments.Select(p => new TaxReceiptLineDto
            {
                PaymentDate = p.PaymentDate,
                Reference = p.Reference,
                PlanLabel = p.Plan == PaymentPlan.Season
                    ? (lang == "fr" ? "Forfait de saison" : "Season pass")
                    : (lang == "fr" ? "Séance à la carte" : "Drop-in"),
                Amount = p.Amount,
            }).ToList(),
        };

        var generator = new TaxReceiptPdfGenerator(_webHostEnvironment);
        var pdf = generator.Generate(receipt, lang);

        _logger.LogInformation("Tax receipt generated for user {UserId}, year {Year}", userId, year);
        return (pdf, $"shb-payment-summary-{year}.pdf");
    }
}

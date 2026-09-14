using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.PromoCodes;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class PromoCodeService : IPromoCodeService
{
    private const int MaxCodeLength = 32; // PromoCodes.Code column length

    private readonly IPromoCodeRepository _repository;
    private readonly ILogger<PromoCodeService> _logger;

    public PromoCodeService(IPromoCodeRepository repository, ILogger<PromoCodeService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PromoCodeDto>> GetAllAsync()
    {
        var all = await _repository.GetAllAsync();
        return all.Select(ToDto).ToList();
    }

    public async Task<PromoCodeDto> CreateAsync(UpsertPromoCodeDto body)
    {
        Validate(body);
        var code = body.Code.Trim().ToUpperInvariant();
        if (await _repository.GetByCodeAsync(code) is not null)
            throw new ValidationException("A promo code with that value already exists");

        var entity = new PromoCode(code, body.DiscountType, body.DiscountValue,
            body.ValidFrom, body.ValidUntil, body.AppliesTo, body.MaxUses, body.IsActive);

        // The pre-check above can race another admin creating the same code.
        if (!await _repository.TryAddAsync(entity))
            throw new ValidationException("A promo code with that value already exists");
        return ToDto(entity);
    }

    public async Task<PromoCodeDto> UpdateAsync(Guid id, UpsertPromoCodeDto body)
    {
        Validate(body);
        var entity = await _repository.GetByIdAsync(id)
            ?? throw new NotFoundException($"Promo code {id} not found");

        entity.DiscountType = body.DiscountType;
        entity.DiscountValue = body.DiscountValue;
        entity.ValidFrom = body.ValidFrom;
        entity.ValidUntil = body.ValidUntil;
        entity.AppliesTo = body.AppliesTo;
        entity.MaxUses = body.MaxUses;
        entity.IsActive = body.IsActive;

        await _repository.UpdateAsync(entity);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id)
            ?? throw new NotFoundException($"Promo code {id} not found");
        if (await _repository.IsUsedByPaymentsAsync(entity.Id))
            throw new ValidationException("This promo code has been used on payments. Deactivate it instead of deleting it.");
        await _repository.DeleteAsync(entity.Id);
    }

    public async Task<ValidatePromoCodeResultDto> ValidateAsync(ValidatePromoCodeDto body)
    {
        if (body.Amount < 0)
            throw new ValidationException("Amount cannot be negative");

        var result = new ValidatePromoCodeResultDto
        {
            OriginalAmount = body.Amount,
            FinalAmount = body.Amount,
        };

        if (string.IsNullOrWhiteSpace(body.Code))
        {
            result.Reason = "Code is required";
            return result;
        }

        var code = body.Code.Trim().ToUpperInvariant();
        var promo = await _repository.GetByCodeAsync(code);
        if (promo is null) { result.Reason = "Code not found"; return result; }
        if (!promo.IsActive) { result.Reason = "Code is inactive"; return result; }

        var now = DateTime.UtcNow;
        if (now < promo.ValidFrom) { result.Reason = "Code is not yet valid"; return result; }
        if (now > promo.ValidUntil) { result.Reason = "Code has expired"; return result; }
        if (promo.MaxUses is int max && promo.TimesUsed >= max) { result.Reason = "Code has been used up"; return result; }

        if (promo.AppliesTo != PromoAppliesTo.Both && promo.AppliesTo != body.TargetPlan)
        {
            result.Reason = "Code does not apply to this plan";
            return result;
        }

        var discount = PaymentPricing.CalculateDiscount(promo, body.Amount);

        result.Valid = true;
        result.DiscountAmount = discount;
        result.FinalAmount = Math.Max(0, body.Amount - discount);
        return result;
    }

    private static void Validate(UpsertPromoCodeDto body)
    {
        if (string.IsNullOrWhiteSpace(body.Code))
            throw new ValidationException("Code is required");
        if (body.Code.Trim().Length > MaxCodeLength)
            throw new ValidationException($"Code cannot be longer than {MaxCodeLength} characters");
        if (!Enum.IsDefined(body.DiscountType))
            throw new ValidationException("Discount type must be Percent (0) or Fixed (1)");
        if (!Enum.IsDefined(body.AppliesTo))
            throw new ValidationException("Applies-to must be DropIn (0), Season (1) or Both (2)");
        if (body.DiscountValue <= 0)
            throw new ValidationException("Discount value must be positive");
        if (body.DiscountType == PromoDiscountType.Percent && body.DiscountValue > 100)
            throw new ValidationException("Percentage discount cannot exceed 100");
        if (body.MaxUses is < 1)
            throw new ValidationException("Max uses must be at least 1 when set");
        if (body.ValidUntil <= body.ValidFrom)
            throw new ValidationException("Valid-until must be after valid-from");
    }

    private static PromoCodeDto ToDto(PromoCode p) => new()
    {
        Id = p.Id,
        Code = p.Code,
        DiscountType = p.DiscountType,
        DiscountValue = p.DiscountValue,
        ValidFrom = p.ValidFrom,
        ValidUntil = p.ValidUntil,
        MaxUses = p.MaxUses,
        TimesUsed = p.TimesUsed,
        AppliesTo = p.AppliesTo,
        IsActive = p.IsActive,
        CreatedOn = p.CreatedOn,
    };
}

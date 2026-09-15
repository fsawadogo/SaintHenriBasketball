using SaintHenriBasketball.Application.DTOs.Credits;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class AccountCreditService : IAccountCreditService
{
    public const decimal MaxAdjustment = 500m;
    public const int MaxNoteLength = 300;

    private readonly IAccountCreditRepository _repository;
    private readonly IUserRepository _users;

    public AccountCreditService(IAccountCreditRepository repository, IUserRepository users)
    {
        _repository = repository;
        _users = users;
    }

    public async Task<AccountCreditsDto> GetForUserAsync(Guid userId)
    {
        var entries = await _repository.GetByUserAsync(userId);
        return new AccountCreditsDto
        {
            // Summed from the same rows that are listed, so the two always agree.
            Balance = entries.Sum(e => e.Amount),
            Entries = entries.Select(e => new AccountCreditEntryDto
            {
                Id = e.Id,
                Amount = e.Amount,
                Kind = e.Kind,
                CreatedAt = e.CreatedAt,
                PaymentId = e.PaymentId,
                Note = e.Note,
            }).ToList(),
        };
    }

    public async Task<AccountCreditsDto> AdjustAsync(Guid userId, decimal amount, string? note, Guid? adminUserId)
    {
        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        if (amount == 0m)
            throw new ValidationException("Enter an amount other than zero.");
        if (Math.Abs(amount) > MaxAdjustment)
            throw new ValidationException($"Adjust at most ${MaxAdjustment:0} at a time.");
        note = note?.Trim();
        if (string.IsNullOrEmpty(note))
            throw new ValidationException("Add a note explaining the adjustment.");
        if (note.Length > MaxNoteLength)
            throw new ValidationException($"Keep the note under {MaxNoteLength} characters.");
        if (await _users.GetByIdAsync(userId) is null)
            throw new NotFoundException("User not found");

        if (amount < 0m)
        {
            var balance = await _repository.GetBalanceAsync(userId);
            if (balance + amount < 0m)
                throw new ValidationException($"This would take the balance below zero. The player has ${balance:0.00} of credit.");
        }

        await _repository.TryAddAsync(new AccountCredit(userId, amount, AccountCreditKind.ManualAdjustment, note: note, createdByUserId: adminUserId));
        return await GetForUserAsync(userId);
    }
}

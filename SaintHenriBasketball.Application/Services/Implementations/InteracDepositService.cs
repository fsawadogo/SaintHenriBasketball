using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.InteracDeposits;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

/// <summary>
/// Turns a forwarded Interac confirmation into a matched payment.
///
/// Players are asked to put their payment reference (DROPIN-2605-1591) in the transfer message, and
/// the bank repeats it in the confirmation email. When that reference names exactly one pending
/// payment and the amount agrees, the payment is marked paid without anyone comparing anything by
/// hand. Everything else is left for an admin: a wrong amount, an unknown sender, two payments
/// sharing a reference. Money is never marked paid on a guess.
/// </summary>
public class InteracDepositService : IInteracDepositService
{
    /// A deposit can only pay a payment created in this window; older pending payments are chased, not matched.
    public static readonly TimeSpan MatchWindow = TimeSpan.FromDays(60);
    public const int MaxBodyLength = 8000;
    public const int MaxNoteLength = 300;
    public const int DefaultListLimit = 100;

    /// The club's own payment references: DROPIN-2605-1591 or SEASON-2609-4820.
    private static readonly Regex PaymentReference = new(@"\b(?:DROPIN|SEASON)-\d{4}-\d{3,5}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private const string InteracReferenceMarker = "|INTERAC:";

    private readonly IInteracDepositRepository _repository;
    private readonly IPaymentService _paymentService;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<InteracDepositService> _logger;

    public InteracDepositService(
        IInteracDepositRepository repository,
        IPaymentService paymentService,
        IAuditLogService auditLog,
        ILogger<InteracDepositService> logger)
    {
        _repository = repository;
        _paymentService = paymentService;
        _auditLog = auditLog;
        _logger = logger;
    }

    public async Task<IngestInteracEmailResultDto> IngestAsync(IngestInteracEmailDto email)
    {
        var parsed = InteracEmailParser.Parse(email.Subject ?? string.Empty, email.Body ?? string.Empty);
        if (parsed == null) return new IngestInteracEmailResultDto { Outcome = "not-a-deposit" };

        var receivedAt = email.ReceivedAt?.UtcDateTime ?? DateTime.UtcNow;
        var fingerprint = Fingerprint(parsed, receivedAt);

        var existing = await _repository.FindDuplicateAsync(fingerprint, email.MessageId);
        if (existing != null)
        {
            // Forwarding rules resend; the same money must never be counted twice.
            return new IngestInteracEmailResultDto
            {
                Outcome = "duplicate",
                DepositId = existing.Id,
                Status = existing.Status,
                Confidence = existing.Confidence,
                MatchedPaymentId = existing.MatchedPaymentId,
            };
        }

        var body = (email.Body ?? string.Empty).Length > MaxBodyLength
            ? InteracEmailParser.ToPlainText(email.Body!)[..Math.Min(MaxBodyLength, InteracEmailParser.ToPlainText(email.Body!).Length)]
            : InteracEmailParser.ToPlainText(email.Body ?? string.Empty);

        var deposit = new InteracDeposit(receivedAt, parsed.Amount, parsed.SenderName, parsed.ReferenceNumber,
            (email.Subject ?? string.Empty).Trim(), body, fingerprint, email.MessageId)
        {
            Note = parsed.Message,
        };

        var (payment, confidence, _) = await FindMatchAsync(parsed, receivedAt);

        if (payment != null && confidence == InteracMatchConfidence.Exact)
        {
            await _paymentService.UpdatePaymentStatusAsync(payment.Id, PaymentStatus.Completed);
            deposit.Status = InteracDepositStatus.Matched;
            deposit.Confidence = confidence;
            deposit.MatchedPaymentId = payment.Id;
            deposit.MatchedOn = DateTime.UtcNow;
            await _auditLog.LogAsync("Payment.InteracAutoMatched", "Payment", payment.Id,
                $"Deposit {parsed.Amount:0.00} from {parsed.SenderName} matched payment {payment.Reference}", userName: "Interac deposit");
            _logger.LogInformation("Interac deposit matched payment {PaymentId} automatically", payment.Id);
        }
        else
        {
            deposit.Confidence = payment != null ? InteracMatchConfidence.Likely : InteracMatchConfidence.None;
        }

        await _repository.AddAsync(deposit);
        await _repository.SaveAsync();

        return new IngestInteracEmailResultDto
        {
            Outcome = "parsed",
            DepositId = deposit.Id,
            Status = deposit.Status,
            Confidence = deposit.Confidence,
            MatchedPaymentId = deposit.MatchedPaymentId,
        };
    }

    public async Task<IReadOnlyList<InteracDepositDto>> ListAsync(bool unmatchedOnly)
    {
        var deposits = await _repository.ListAsync(unmatchedOnly, DefaultListLimit);
        var results = new List<InteracDepositDto>(deposits.Count);

        foreach (var deposit in deposits)
        {
            var dto = new InteracDepositDto
            {
                Id = deposit.Id,
                ReceivedAt = deposit.ReceivedAt,
                Amount = deposit.Amount,
                SenderName = deposit.SenderName,
                Message = deposit.Note,
                ReferenceNumber = deposit.ReferenceNumber,
                Status = deposit.Status.ToString(),
                Confidence = deposit.Confidence.ToString(),
                MatchedPaymentId = deposit.MatchedPaymentId,
                MatchedOn = deposit.MatchedOn,
            };

            if (deposit.MatchedPaymentId is Guid matched)
            {
                dto.PlayerName = await _repository.GetPlayerNameAsync(matched);
            }
            else if (deposit.Status == InteracDepositStatus.Unmatched)
            {
                var parsed = new ParsedInteracDeposit(deposit.Amount, deposit.SenderName, deposit.Note, deposit.ReferenceNumber, null);
                var (payment, confidence, reason) = await FindMatchAsync(parsed, deposit.ReceivedAt);
                if (payment != null)
                {
                    dto.Suggestion = new InteracSuggestionDto
                    {
                        PaymentId = payment.Id,
                        PlayerName = await _repository.GetPlayerNameAsync(payment.Id) ?? string.Empty,
                        Amount = payment.Amount,
                        Reference = payment.Reference,
                        CreatedAt = payment.CreatedAt,
                        Reason = reason,
                    };
                    dto.Confidence = confidence.ToString();
                }
            }

            results.Add(dto);
        }

        return results;
    }

    public async Task<InteracDepositDto> MatchAsync(Guid depositId, Guid paymentId, Guid? adminId, string adminName)
    {
        var deposit = await _repository.GetAsync(depositId) ?? throw new NotFoundException("Deposit not found.");
        if (deposit.Status == InteracDepositStatus.Matched) throw new ValidationException("This deposit is already matched.");

        var candidates = await _repository.GetPendingCandidatesAsync(DateTime.UtcNow - MatchWindow);
        var candidate = candidates.SingleOrDefault(c => c.Payment.Id == paymentId)
            ?? throw new ValidationException("That payment is not pending, or is too old to match.");

        await _paymentService.UpdatePaymentStatusAsync(paymentId, PaymentStatus.Completed);
        deposit.Status = InteracDepositStatus.Matched;
        deposit.MatchedPaymentId = paymentId;
        deposit.MatchedOn = DateTime.UtcNow;
        if (deposit.Confidence == InteracMatchConfidence.None) deposit.Confidence = InteracMatchConfidence.Likely;
        await _repository.SaveAsync();

        await _auditLog.LogAsync("Payment.InteracDepositMatched", "Payment", candidate.Payment.Id,
            $"Deposit {deposit.Amount:0.00} from {deposit.SenderName} matched payment {candidate.Payment.Reference}", adminId, adminName);

        return new InteracDepositDto
        {
            Id = deposit.Id,
            ReceivedAt = deposit.ReceivedAt,
            Amount = deposit.Amount,
            SenderName = deposit.SenderName,
            Message = deposit.Note,
            ReferenceNumber = deposit.ReferenceNumber,
            Status = deposit.Status.ToString(),
            Confidence = deposit.Confidence.ToString(),
            MatchedPaymentId = deposit.MatchedPaymentId,
            MatchedOn = deposit.MatchedOn,
            PlayerName = candidate.PlayerName,
        };
    }

    public async Task IgnoreAsync(Guid depositId, string? note, Guid? adminId, string adminName)
    {
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (note?.Length > MaxNoteLength) throw new ValidationException($"Keep the note under {MaxNoteLength} characters.");

        var deposit = await _repository.GetAsync(depositId) ?? throw new NotFoundException("Deposit not found.");
        if (deposit.Status == InteracDepositStatus.Matched) throw new ValidationException("A matched deposit cannot be set aside; undo the match first.");

        deposit.Status = InteracDepositStatus.Ignored;
        deposit.Note = note ?? deposit.Note;
        await _repository.SaveAsync();
        await _auditLog.LogAsync("Payment.InteracDepositIgnored", "InteracDeposit", depositId,
            $"Deposit {deposit.Amount:0.00} from {deposit.SenderName} set aside", adminId, adminName);
    }

    /// <returns>The payment to match, how sure we are, and why — in words an admin can read.</returns>
    private async Task<(Payment? Payment, InteracMatchConfidence Confidence, string Reason)> FindMatchAsync(ParsedInteracDeposit parsed, DateTime receivedAt)
    {
        var candidates = await _repository.GetPendingCandidatesAsync(receivedAt - MatchWindow);
        if (candidates.Count == 0) return (null, InteracMatchConfidence.None, string.Empty);

        // 1. The reference the player was asked to write in the transfer message.
        var reference = PaymentReference.Match(parsed.Message ?? string.Empty);
        if (reference.Success)
        {
            var byReference = candidates
                .Where(c => ReferenceHead(c.Payment.Reference).Equals(reference.Value, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (byReference.Count == 1)
            {
                var match = byReference[0].Payment;
                return match.Amount == parsed.Amount
                    ? (match, InteracMatchConfidence.Exact, $"Reference {reference.Value} and the amount both match")
                    // Right player, wrong money: an admin decides whether it settles the payment.
                    : (match, InteracMatchConfidence.Likely, $"Reference {reference.Value} matches, but the deposit is {parsed.Amount:0.00} and the payment is {match.Amount:0.00}");
            }

            if (byReference.Count > 1)
            {
                return (null, InteracMatchConfidence.None, $"Two payments carry the reference {reference.Value}");
            }
        }

        // 2. The Interac reference the player typed into the app when they submitted the transfer.
        if (!string.IsNullOrWhiteSpace(parsed.ReferenceNumber))
        {
            var submitted = candidates
                .Where(c => SubmittedInteracReference(c.Payment.Reference).Equals(parsed.ReferenceNumber, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (submitted.Count == 1)
            {
                var match = submitted[0].Payment;
                return match.Amount == parsed.Amount
                    ? (match, InteracMatchConfidence.Exact, $"The player submitted reference {parsed.ReferenceNumber}, and the amount matches")
                    : (match, InteracMatchConfidence.Likely, $"The player submitted reference {parsed.ReferenceNumber}, but the amounts differ");
            }
        }

        // 3. Nothing to go on but the name and the amount: a suggestion, never an automatic match.
        var byName = candidates
            .Where(c => c.Payment.Amount == parsed.Amount && NamesMatch(c.PlayerName, parsed.SenderName))
            .ToList();
        if (byName.Count == 1)
        {
            return (byName[0].Payment, InteracMatchConfidence.Likely, $"{parsed.SenderName} owes exactly {parsed.Amount:0.00}");
        }

        return (null, InteracMatchConfidence.None, string.Empty);
    }

    /// The club's reference, before any submitted Interac reference was appended to it.
    private static string ReferenceHead(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return string.Empty;
        var marker = reference.IndexOf(InteracReferenceMarker, StringComparison.OrdinalIgnoreCase);
        return (marker >= 0 ? reference[..marker] : reference).Trim();
    }

    private static string SubmittedInteracReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return string.Empty;
        var marker = reference.IndexOf(InteracReferenceMarker, StringComparison.OrdinalIgnoreCase);
        return marker < 0 ? string.Empty : reference[(marker + InteracReferenceMarker.Length)..].Trim();
    }

    /// Compares names ignoring case, accents, punctuation and word order: "José-Colombe Koffi" is "koffi jose colombe".
    public static bool NamesMatch(string? left, string? right)
    {
        var a = NameWords(left);
        var b = NameWords(right);
        if (a.Count == 0 || b.Count == 0) return false;
        return a.SetEquals(b) || a.IsSubsetOf(b) || b.IsSubsetOf(a);
    }

    private static HashSet<string> NameWords(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return new HashSet<string>();
        var stripped = new string(name.Normalize(NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());
        return stripped.ToLowerInvariant()
            .Split(new[] { ' ', '-', '\'', '.', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length > 1)
            .ToHashSet();
    }

    /// Identifies one email by what it says, so the same deposit forwarded twice is stored once.
    private static string Fingerprint(ParsedInteracDeposit parsed, DateTime receivedAt)
    {
        var day = (parsed.SentOn ?? SessionTimeHelper.ToLocal(receivedAt)).Date.ToString("yyyy-MM-dd");
        var raw = string.Join("|", parsed.Amount.ToString("0.00"), parsed.SenderName.Trim().ToLowerInvariant(),
            parsed.ReferenceNumber?.Trim().ToLowerInvariant() ?? string.Empty, parsed.Message?.Trim().ToLowerInvariant() ?? string.Empty, day);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }
}

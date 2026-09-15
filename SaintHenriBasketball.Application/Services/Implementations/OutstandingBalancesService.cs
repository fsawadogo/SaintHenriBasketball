using System.Net;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.OutstandingBalances;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using static SaintHenriBasketball.Application.Helpers.EmailTemplateHelper;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class OutstandingBalancesService : IOutstandingBalancesService
{
    public const int MaxKeysPerRequest = 500;
    public static readonly TimeSpan RepeatWindow = TimeSpan.FromDays(3);
    public const string SkipNoLongerOutstanding = "No longer outstanding";
    public const string SkipDeactivated = "Player is deactivated";
    public const string SkipRemindersOff = "Player turned off payment reminders";
    public const string SkipEmailsOff = "Player turned off email notifications";
    public const string SkipNoEmail = "Player has no email address";
    public const string SkipRecentlyReminded = "Reminder already sent in the last 3 days";
    public const string FailedToSend = "Email could not be sent";

    // Same marker ReconciliationService uses for a player-submitted Interac transfer.
    private const string InteracReferenceMarker = "|INTERAC:";

    private readonly IOutstandingBalancesRepository _repository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IEmailService _emailService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<OutstandingBalancesService> _logger;

    public OutstandingBalancesService(
        IOutstandingBalancesRepository repository,
        IPaymentRepository paymentRepository,
        IEmailService emailService,
        IAuditLogService auditLogService,
        ILogger<OutstandingBalancesService> logger)
    {
        _repository = repository;
        _paymentRepository = paymentRepository;
        _emailService = emailService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    private sealed record Debt(
        string Key, ApplicationUser User, string Kind, decimal Amount, DateTime Since,
        Guid? PaymentId = null, Guid? SessionId = null, DateTime? SessionStart = null, string? SessionStartTime = null,
        Guid? SeasonId = null, string? SeasonName = null, string? InteracReference = null);

    public async Task<OutstandingBalancesDto> GetBalancesAsync(OutstandingBalancesQuery query)
    {
        var kind = ParseKind(query.Kind);
        var sort = (query.Sort ?? "age").Trim().ToLowerInvariant();
        if (sort is not ("age" or "amount")) throw new ValidationException("Sort by \"age\" or \"amount\".");
        var direction = (query.Direction ?? "desc").Trim().ToLowerInvariant();
        if (direction is not ("asc" or "desc")) throw new ValidationException("Direction must be \"asc\" or \"desc\".");

        var now = DateTime.UtcNow;
        var debts = await LoadDebtsAsync(now);
        var lastSent = await _repository.GetLastSentAsync(debts.Select(d => d.User.Id).Distinct().ToList());

        var rows = debts.Where(d => kind == null || d.Kind == kind).Select(d => ToRow(d, now, lastSent));
        var descending = direction == "desc";
        var ordered = sort == "amount"
            // Descending amount = largest first; descending age = oldest first.
            ? (descending ? rows.OrderByDescending(r => r.Amount) : rows.OrderBy(r => r.Amount))
            : (descending ? rows.OrderBy(r => r.Since) : rows.OrderByDescending(r => r.Since));

        return new OutstandingBalancesDto
        {
            Totals = new BalanceTotalsDto
            {
                SeasonFee = Total(debts.Where(d => d.Kind == ReminderKinds.SeasonFee)),
                DropIn = Total(debts.Where(d => d.Kind == ReminderKinds.DropIn)),
                Transfer = Total(debts.Where(d => d.Kind == ReminderKinds.Transfer)),
                All = Total(debts),
            },
            Items = ordered.ThenBy(r => r.PlayerName, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Key).ToList(),
            GeneratedAt = now,
        };
    }

    public async Task<SendRemindersResultDto> SendRemindersAsync(SendRemindersRequestDto request, Guid? adminId, string adminName)
    {
        var kind = ParseKind(request.Kind);
        var keys = (request.Keys ?? new List<string>()).Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).Distinct().ToList();
        if (keys.Count == 0 && kind == null) throw new ValidationException("Select at least one balance, or a kind to remind.");
        if (keys.Count > MaxKeysPerRequest) throw new ValidationException($"Send at most {MaxKeysPerRequest} reminders at a time.");

        var now = DateTime.UtcNow;
        var debts = await LoadDebtsAsync(now);
        var result = new SendRemindersResultDto();

        List<Debt> selected;
        if (keys.Count > 0)
        {
            var byKey = debts.ToDictionary(d => d.Key);
            selected = new List<Debt>();
            foreach (var key in keys)
            {
                // The list the admin selected from can be stale: a debt paid since is simply skipped.
                if (byKey.TryGetValue(key, out var debt) && (kind == null || debt.Kind == kind)) selected.Add(debt);
                else result.SkippedItems.Add(new ReminderOutcomeDto { Key = key, Reason = SkipNoLongerOutstanding });
            }
        }
        else
        {
            selected = debts.Where(d => d.Kind == kind).ToList();
        }

        var recent = await _repository.GetLastSentAsync(selected.Select(d => d.User.Id).Distinct().ToList(), now - RepeatWindow);
        var skippedLogs = new List<ReminderLog>();
        var toSend = new List<Debt>();
        foreach (var debt in selected)
        {
            var reason = SkipReason(debt, recent);
            if (reason == null) { toSend.Add(debt); continue; }
            skippedLogs.Add(NewLog(debt, ReminderStatuses.Skipped, reason, now, adminId));
            result.SkippedItems.Add(Outcome(debt, reason));
        }
        if (skippedLogs.Count > 0) await _repository.AddLogsAsync(skippedLogs);

        // One email per player, covering every selected debt of theirs.
        foreach (var group in toSend.GroupBy(d => d.User.Id))
        {
            var playerDebts = group.ToList();
            var user = playerDebts[0].User;
            string status;
            try
            {
                var language = user.PreferredLanguage;
                await _emailService.SendEmailAsync(
                    user.Email,
                    LSubject("Payment reminder - Saint-Henri Basketball", "Rappel de paiement - Saint-Henri Basketball", language),
                    BuildReminderEmail(user, playerDebts, language));
                status = ReminderStatuses.Sent;
                result.Sent += playerDebts.Count;
                result.EmailsSent++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Payment reminder email failed for player {UserId}", user.Id);
                status = ReminderStatuses.Failed;
                result.Failed += playerDebts.Count;
                result.FailedItems.AddRange(playerDebts.Select(d => Outcome(d, FailedToSend)));
            }

            // Saved right after each email, so the 3-day rule holds even if a later send stops the request.
            await _repository.AddLogsAsync(playerDebts.Select(d =>
                NewLog(d, status, status == ReminderStatuses.Failed ? FailedToSend : null, DateTime.UtcNow, adminId)).ToList());
        }

        result.Skipped = result.SkippedItems.Count;
        await WriteAuditAsync(result, kind, keys.Count, adminId, adminName);
        return result;
    }

    public async Task<ReminderLogPageDto> GetReminderHistoryAsync(Guid? userId, int page, int pageSize)
    {
        if (userId is Guid id && !await _repository.UserExistsAsync(id))
            throw new NotFoundException("Player", id);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var (items, total) = await _repository.SearchLogsAsync(userId, page, pageSize);
        return new ReminderLogPageDto
        {
            Items = items.Select(e => new ReminderLogDto
            {
                Id = e.Log.Id,
                UserId = e.Log.UserId,
                PlayerName = PlayerName(e.FirstName, e.LastName, e.Email),
                Email = e.Email,
                PaymentId = e.Log.PaymentId,
                SeasonId = e.Log.SeasonId,
                Kind = e.Log.Kind,
                Channel = e.Log.Channel,
                Status = e.Log.Status,
                Reason = e.Log.Reason,
                SentAt = Utc(e.Log.SentAt),
                SentByUserId = e.Log.SentByUserId,
            }).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    private async Task<List<Debt>> LoadDebtsAsync(DateTime now)
    {
        var todayLocal = SessionTimeHelper.ToLocal(now).Date;
        var debts = new List<Debt>();

        // Drop-ins and transfers: every pending payment of an active player.
        foreach (var payment in await _paymentRepository.GetPaymentsByStatusAsync(PaymentStatus.Pending))
        {
            if (payment.User is null || payment.User.IsDeactivated || payment.Amount <= 0) continue;
            var session = payment.Session;
            DateTime? sessionStart = session is null
                ? null
                : SessionTimeHelper.ToUtc(SessionTimeHelper.CombineLocal(session.SessionDate, session.StartTime));

            if (IsInteracSubmission(payment.Reference))
            {
                debts.Add(new Debt($"transfer:{payment.Id}", payment.User, ReminderKinds.Transfer, payment.Amount, Utc(payment.PaymentDate),
                    PaymentId: payment.Id, SessionId: payment.SessionId, SessionStart: sessionStart, SessionStartTime: session?.StartTime,
                    SeasonId: payment.SeasonId, SeasonName: payment.Season?.Name,
                    InteracReference: payment.Reference![(payment.Reference!.IndexOf(InteracReferenceMarker, StringComparison.Ordinal) + InteracReferenceMarker.Length)..]));
                continue;
            }

            if (payment.Plan != PaymentPlan.DropIn) continue;
            // A drop-in is owed once its session day has come, and never for a cancelled session.
            if (session != null && (session.Status == SessionStatus.Cancelled || session.SessionDate.Date > todayLocal)) continue;
            debts.Add(new Debt($"dropin:{payment.Id}", payment.User, ReminderKinds.DropIn, payment.Amount,
                sessionStart ?? Utc(payment.PaymentDate),
                PaymentId: payment.Id, SessionId: payment.SessionId, SessionStart: sessionStart, SessionStartTime: session?.StartTime));
        }

        debts.AddRange(await LoadSeasonFeesAsync(now, todayLocal));
        return debts;
    }

    private async Task<List<Debt>> LoadSeasonFeesAsync(DateTime now, DateTime todayLocal)
    {
        var seasons = await _repository.GetBillableSeasonsAsync(todayLocal);
        if (seasons.Count == 0) return new List<Debt>();
        var seasonIds = seasons.Select(s => s.Id).ToList();

        // Season players: those registered for a billable season, plus Season-plan players for the season running today.
        // Like the player's own season billing, a Season-plan player is only matched when exactly one season runs today.
        var candidates = new Dictionary<(Guid SeasonId, Guid UserId), (Season Season, ApplicationUser User, DateTime? RegisteredOn)>();
        foreach (var registration in await _repository.GetSeasonRegistrationsAsync(seasonIds))
        {
            if (registration.User is null || registration.User.IsDeactivated) continue;
            var season = seasons.First(s => s.Id == registration.SeasonId);
            candidates[(season.Id, registration.UserId)] = (season, registration.User, Utc(registration.RegisteredOn));
        }
        var running = seasons.Where(s => s.StartDate.Date <= todayLocal && s.EndDate.Date >= todayLocal).ToList();
        if (running.Count == 1)
        {
            foreach (var player in await _repository.GetActiveSeasonPlanPlayersAsync())
                candidates.TryAdd((running[0].Id, player.Id), (running[0], player, null));
        }

        var payments = (await _repository.GetSeasonPaymentsAsync(seasonIds)).ToLookup(p => (p.SeasonId!.Value, p.UserId));
        var unlinked = (await _repository.GetUsersWithUnlinkedSeasonPaymentsAsync()).ToHashSet();
        var debts = new List<Debt>();
        foreach (var ((seasonId, userId), (season, user, registeredOn)) in candidates)
        {
            var records = payments[(seasonId, userId)].ToList();
            if (records.Any(p => p.Status == PaymentStatus.Completed)) continue;
            var pending = records.Where(p => p.Status == PaymentStatus.Pending).ToList();
            // A submitted transfer is listed under transfers awaiting review.
            if (pending.Any(p => IsInteracSubmission(p.Reference))) continue;
            // Never guess which season an older payment without a season was for.
            if (pending.Count == 0 && unlinked.Contains(userId)) continue;

            var amount = pending.Count > 0 ? pending.Sum(p => p.Amount) : season.Price;
            if (amount <= 0) continue;
            var start = SessionTimeHelper.ToUtc(season.StartDate.Date);
            var since = registeredOn is DateTime registered
                ? (start <= now ? (registered > start ? registered : start) : registered)
                : start;
            debts.Add(new Debt($"season:{seasonId}:{userId}", user, ReminderKinds.SeasonFee, amount, since,
                PaymentId: pending.OrderByDescending(p => p.PaymentDate).FirstOrDefault()?.Id,
                SeasonId: seasonId, SeasonName: season.Name));
        }
        return debts;
    }

    private static string? SkipReason(Debt debt, IReadOnlyList<ReminderLastSent> recent)
    {
        var user = debt.User;
        if (user.IsDeactivated) return SkipDeactivated;
        if (!user.PaymentRemindersEnabled) return SkipRemindersOff;
        if (!user.EmailNotificationsEnabled) return SkipEmailsOff;
        if (string.IsNullOrWhiteSpace(user.Email)) return SkipNoEmail;
        if (LastSentFor(debt, recent) != null) return SkipRecentlyReminded;
        return null;
    }

    /// The same debt: same player and kind, and the same season (season fees) or payment (drop-ins, transfers).
    private static DateTime? LastSentFor(Debt debt, IReadOnlyList<ReminderLastSent> sent) =>
        sent.Where(s => s.UserId == debt.User.Id && s.Kind == debt.Kind
                && (debt.Kind == ReminderKinds.SeasonFee ? s.SeasonId == debt.SeasonId : s.PaymentId == debt.PaymentId))
            .Select(s => (DateTime?)s.SentAt)
            .Max();

    private static OutstandingBalanceRowDto ToRow(Debt d, DateTime now, IReadOnlyList<ReminderLastSent> lastSent) => new()
    {
        Key = d.Key,
        UserId = d.User.Id,
        PlayerName = PlayerName(d.User.FirstName, d.User.LastName, d.User.Email),
        Email = d.User.Email,
        Kind = d.Kind,
        Amount = d.Amount,
        Since = d.Since,
        DaysOutstanding = Math.Max(0, (SessionTimeHelper.ToLocal(now).Date - SessionTimeHelper.ToLocal(d.Since).Date).Days),
        PaymentId = d.PaymentId,
        SessionId = d.SessionId,
        SessionDate = d.SessionStart,
        SeasonId = d.SeasonId,
        SeasonName = d.SeasonName,
        InteracReference = d.InteracReference,
        LastReminderSentAt = LastSentFor(d, lastSent),
        CanRemind = d.User.PaymentRemindersEnabled && d.User.EmailNotificationsEnabled && !string.IsNullOrWhiteSpace(d.User.Email),
    };

    private static string BuildReminderEmail(ApplicationUser user, IReadOnlyList<Debt> debts, EmailLanguage lang)
    {
        string Money(decimal amount) => amount.ToString("0.00", GetCulture(lang)) + " $";
        var content = Greeting(WebUtility.HtmlEncode(user.FirstName ?? ""), lang)
            + P(L("Our records show the following balance with Saint-Henri Basketball.",
                  "Nos dossiers indiquent le solde suivant auprès de Saint-Henri Basketball.", lang));

        foreach (var debt in debts)
        {
            var fields = new Dictionary<string, string?>();
            switch (debt.Kind)
            {
                case ReminderKinds.SeasonFee:
                    fields[L("Season fee", "Frais de saison", lang)] = WebUtility.HtmlEncode(debt.SeasonName ?? "");
                    break;
                case ReminderKinds.DropIn:
                    fields[L("Drop-in session", "Séance à la carte", lang)] = debt.SessionStart is DateTime start
                        ? SessionTimeHelper.ToLocal(start).ToString("yyyy-MM-dd HH:mm")
                        : Utc(debt.Since).ToString("yyyy-MM-dd");
                    break;
                default:
                    fields[L("Interac transfer awaiting confirmation", "Virement Interac en attente de confirmation", lang)] =
                        WebUtility.HtmlEncode(debt.InteracReference ?? "");
                    break;
            }
            fields[L("Amount due", "Montant dû", lang)] = Money(debt.Amount);
            content += BuildInfoBox(fields);
        }

        if (debts.Any(d => d.Kind != ReminderKinds.Transfer))
            content += P(L("Send an Interac e-Transfer to <strong>pay@sainthenribasketball.com</strong>, or pay from your account.",
                           "Envoyez un virement Interac à <strong>pay@sainthenribasketball.com</strong>, ou payez depuis votre compte.", lang));
        if (debts.Any(d => d.Kind == ReminderKinds.Transfer))
            content += P(L("We haven't been able to match your Interac transfer yet. Please check that it was sent and that the confirmation number is correct.",
                           "Nous n'avons pas encore pu associer votre virement Interac. Veuillez vérifier qu'il a bien été envoyé et que le numéro de confirmation est exact.", lang));
        content += P(L("If you've already paid, you can ignore this message.", "Si vous avez déjà payé, vous pouvez ignorer ce message.", lang));
        return BuildEmailLayout("Payment Reminder", "Rappel de paiement", content, lang);
    }

    private async Task WriteAuditAsync(SendRemindersResultDto result, string? kind, int keyCount, Guid? adminId, string adminName)
    {
        try
        {
            var scope = keyCount > 0 ? $"Selected: {keyCount}" : $"Kind: {kind}";
            await _auditLogService.LogAsync("OutstandingBalances.RemindersSent", nameof(ReminderLog), null,
                $"{scope}; Sent: {result.Sent}; Skipped: {result.Skipped}; Failed: {result.Failed}; Emails: {result.EmailsSent}",
                adminId, adminName);
        }
        catch (Exception ex)
        {
            // The reminders already went out; a missing audit row must not report them as an error.
            _logger.LogWarning(ex, "Audit entry failed for payment reminders");
        }
    }

    private static string? ParseKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind)) return null;
        return ReminderKinds.All.FirstOrDefault(k => string.Equals(k, kind.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ValidationException("Kind must be SeasonFee, DropIn or Transfer.");
    }

    private static ReminderLog NewLog(Debt debt, string status, string? reason, DateTime at, Guid? adminId) => new()
    {
        UserId = debt.User.Id,
        PaymentId = debt.PaymentId,
        SeasonId = debt.SeasonId,
        Kind = debt.Kind,
        Channel = ReminderChannels.Email,
        Status = status,
        Reason = reason,
        SentAt = at,
        SentByUserId = adminId,
    };

    private static ReminderOutcomeDto Outcome(Debt debt, string reason) => new()
    {
        Key = debt.Key,
        UserId = debt.User.Id,
        PlayerName = PlayerName(debt.User.FirstName, debt.User.LastName, debt.User.Email),
        Reason = reason,
    };

    private static BalanceKindTotalDto Total(IEnumerable<Debt> debts)
    {
        var list = debts.ToList();
        return new BalanceKindTotalDto { Count = list.Count, Amount = list.Sum(d => d.Amount) };
    }

    private static string PlayerName(string? first, string? last, string? email)
    {
        var name = $"{first} {last}".Trim();
        return string.IsNullOrWhiteSpace(name) ? (email ?? "(unknown)") : name;
    }

    private static bool IsInteracSubmission(string? reference) =>
        reference?.Contains(InteracReferenceMarker, StringComparison.Ordinal) == true;

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

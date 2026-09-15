using System.Globalization;
using System.Reflection;
using SaintHenriBasketball.Application.DTOs.PlayerTimeline;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using T = SaintHenriBasketball.Application.DTOs.PlayerTimeline.PlayerTimelineEventTypes;

namespace SaintHenriBasketball.Application.Services.Implementations;

/// <summary>
/// Builds a player's timeline from payments, credits, referrals, waivers, attendance, emails, notifications and
/// audit entries. A page is served by asking each selected source only for its newest page × pageSize rows
/// (the most any one source can contribute to that window), merging them, and cutting the page out of the merge.
/// </summary>
public class PlayerTimelineService : IPlayerTimelineService
{
    public const int MaxPageSize = 100;
    /// page × pageSize may not exceed this, so a deep page can't make every source load thousands of rows.
    public const int MaxWindow = 5000;
    public const string WindowTooDeepMessage = "That page is too far back. Filter by type to reach older events.";

    /// Audit actions that change AdminNotes (the notes endpoint and the tags endpoint, which stores tags in the notes).
    public static readonly IReadOnlyList<string> NoteActions = new[] { "UpdateNotes", "UpdateUserTags" };

    private const int MaxBodyPreview = 200;

    // Another feature may add an outcome to attendance; read it when it exists without depending on it.
    private static readonly PropertyInfo? AttendanceOutcome = typeof(SessionAttendance).GetProperty("Outcome");

    private readonly IPlayerTimelineRepository _repository;

    public PlayerTimelineService(IPlayerTimelineRepository repository)
    {
        _repository = repository;
    }

    public async Task<PlayerTimelineDto> GetTimelineAsync(Guid userId, PlayerTimelineQuery query)
    {
        var player = await _repository.GetPlayerAsync(userId) ?? throw new NotFoundException("User", userId);
        var types = ParseTypes(query.Types);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        if ((long)page * pageSize > MaxWindow) throw new ValidationException(WindowTooDeepMessage);
        var window = page * pageSize;

        var anonymized = player.AnonymizedOn != null;
        var email = anonymized || string.IsNullOrWhiteSpace(player.Email) ? null : player.Email;

        var counts = await _repository.CountAsync(userId, email);
        var countByType = new Dictionary<string, int>
        {
            [T.Payment] = counts.Payments + counts.CompletedPayments + counts.RefundedPayments,
            [T.Credit] = counts.Credits,
            [T.Referral] = counts.ReferralsAsReferrer + counts.ReferralsAsReferee,
            [T.Waiver] = counts.WaiverAcceptances,
            [T.Attendance] = counts.AttendanceAnswers + counts.CheckIns,
            [T.Message] = counts.Messages,
            [T.Notification] = counts.Notifications,
            [T.Admin] = counts.AdminChanges,
        };

        bool Wants(string type) => types.Contains(type) && countByType[type] > 0;
        var payments = Wants(T.Payment) ? await _repository.GetNewestPaymentsAsync(userId, window) : Array.Empty<Payment>();
        var credits = Wants(T.Credit) ? await _repository.GetNewestCreditsAsync(userId, window) : Array.Empty<AccountCredit>();
        var referrals = Wants(T.Referral) ? await _repository.GetNewestReferralsAsync(userId, window) : Array.Empty<ReferralRedemption>();
        var waivers = Wants(T.Waiver) ? await _repository.GetNewestWaiverAcceptancesAsync(userId, window) : Array.Empty<WaiverAcceptance>();
        var attendance = Wants(T.Attendance) ? await _repository.GetNewestAttendanceAsync(userId, window) : Array.Empty<SessionAttendance>();
        var messages = email != null && Wants(T.Message) ? await _repository.GetNewestEmailLogsAsync(email, window) : Array.Empty<EmailLog>();
        var notifications = Wants(T.Notification) ? await _repository.GetNewestNotificationsAsync(userId, window) : Array.Empty<Notification>();
        var audits = Wants(T.Admin) ? await _repository.GetNewestAuditLogsAsync(userId, window) : Array.Empty<AuditLog>();

        var names = await _repository.GetUserNamesAsync(
            credits.Where(c => c.CreatedByUserId != null).Select(c => c.CreatedByUserId!.Value)
                .Concat(messages.Where(m => m.SentByUserId != null).Select(m => m.SentByUserId!.Value))
                .Concat(referrals.Select(r => r.ReferrerUserId == userId ? r.RefereeUserId : r.ReferrerUserId)));
        var codes = await _repository.GetReferralCodesAsync(referrals.Select(r => r.ReferralCodeId));
        var rewards = await _repository.GetReferralRewardsAsync(referrals.Select(r => r.Id));

        var events = new List<PlayerTimelineEventDto>();
        events.AddRange(payments.SelectMany(PaymentEvents));
        events.AddRange(credits.Select(c => CreditEvent(c, names)));
        events.AddRange(referrals.SelectMany(r => ReferralEvents(r, userId, names, codes, rewards)));
        events.AddRange(waivers.Select(WaiverEvent));
        events.AddRange(attendance.SelectMany(AttendanceEvents));
        events.AddRange(messages.Select(m => MessageEvent(m, names)));
        events.AddRange(notifications.Select(NotificationEvent));
        events.AddRange(audits.Select(AdminEvent));

        // Time, then id: a total order, so every window agrees on where each event falls.
        var pageEvents = events
            .OrderByDescending(e => e.OccurredAt)
            .ThenBy(e => e.Id, StringComparer.Ordinal)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var total = types.Sum(t => countByType[t]);
        var notesChange = await _repository.GetLatestAuditAsync(userId, NoteActions);

        return new PlayerTimelineDto
        {
            UserId = player.Id,
            PlayerName = $"{player.FirstName} {player.LastName}".Trim(),
            IsDeactivated = player.IsDeactivated,
            IsAnonymized = anonymized,
            Page = page,
            PageSize = pageSize,
            Total = total,
            HasMore = page * pageSize < total,
            Types = T.All.Where(types.Contains).ToList(),
            Summary = T.All.Select(t => new PlayerTimelineTypeCountDto { Type = t, Count = countByType[t] }).ToList(),
            Events = pageEvents,
            Notes = new PlayerTimelineNotesDto
            {
                Text = player.AdminNotes,
                LastChangedAt = notesChange == null ? null : Utc(notesChange.CreatedAt),
                LastChangedBy = notesChange?.UserName,
            },
            MessagesUnavailable = email == null,
        };
    }

    private static HashSet<string> ParseTypes(IReadOnlyList<string>? requested)
    {
        var parts = (requested ?? Array.Empty<string>())
            .SelectMany(t => (t ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Select(t => t.ToLowerInvariant())
            .ToList();
        if (parts.Count == 0) return new HashSet<string>(T.All);

        var unknown = parts.Where(p => !T.All.Contains(p)).Distinct().ToList();
        if (unknown.Count > 0)
            throw new ValidationException($"Unknown event type: {string.Join(", ", unknown)}. Use {string.Join(", ", T.All)}.");
        return new HashSet<string>(parts);
    }

    private static IEnumerable<PlayerTimelineEventDto> PaymentEvents(Payment p)
    {
        var plan = p.Plan == PaymentPlan.Season ? "Season" : "Drop-in";
        var about = PaymentSubject(p);
        var created = p.CreatedAt < PlayerTimelineTimes.MissingBefore ? p.PaymentDate : p.CreatedAt;
        var adjustments = new List<string>();
        if (p.DiscountAmount > 0) adjustments.Add($"discount {Money(p.DiscountAmount)}");
        if (p.CreditApplied > 0) adjustments.Add($"credit applied {Money(p.CreditApplied)}");

        yield return new PlayerTimelineEventDto
        {
            Id = $"payment:{p.Id}:created",
            Type = T.Payment,
            Subtype = "created",
            OccurredAt = Utc(created),
            Title = $"{plan} payment created",
            Detail = Join(about, $"status {p.Status}", adjustments.Count > 0 ? string.Join(", ", adjustments) : null),
            Amount = p.Amount,
            RelatedId = p.Id,
        };

        if (p.Status is PaymentStatus.Completed or PaymentStatus.Refunded)
        {
            yield return new PlayerTimelineEventDto
            {
                Id = $"payment:{p.Id}:completed",
                Type = T.Payment,
                Subtype = "completed",
                OccurredAt = Utc(p.PaymentDate),
                Title = $"{plan} payment completed",
                Detail = Join(about, p.Reference != null ? $"reference {p.Reference}" : null),
                Amount = p.Amount,
                RelatedId = p.Id,
            };
        }

        if (p.Status == PaymentStatus.Refunded)
        {
            var method = p.RefundMethod switch
            {
                RefundMethod.Card => "refunded to card",
                RefundMethod.AccountCredit => "refunded as account credit",
                RefundMethod.Manual => "refunded outside the app",
                _ => "refund method not recorded",
            };
            yield return new PlayerTimelineEventDto
            {
                Id = $"payment:{p.Id}:refunded",
                Type = T.Payment,
                Subtype = "refunded",
                OccurredAt = Utc(p.RefundedOn ?? p.PaymentDate),
                Title = $"{plan} payment refunded",
                Detail = Join(about, method, string.IsNullOrWhiteSpace(p.RefundReason) ? null : $"reason: {p.RefundReason.Trim()}"),
                Amount = p.Amount,
                RelatedId = p.Id,
            };
        }
    }

    private static string? PaymentSubject(Payment p)
    {
        if (p.Session != null) return $"session {SessionLabel(p.Session)}";
        if (p.Season != null)
            return string.IsNullOrWhiteSpace(p.Season.Name) ? $"season starting {p.Season.StartDate:yyyy-MM-dd}" : $"season {p.Season.Name}";
        return null;
    }

    private static PlayerTimelineEventDto CreditEvent(AccountCredit c, IReadOnlyDictionary<Guid, string> names)
    {
        var title = c.Kind switch
        {
            AccountCreditKind.ReferralReward => "Referral reward credit",
            AccountCreditKind.AppliedToPayment => "Credit applied to a payment",
            AccountCreditKind.Released => "Credit released back",
            AccountCreditKind.Refund => "Payment refunded as credit",
            AccountCreditKind.ManualAdjustment => c.Amount >= 0 ? "Credit added by an admin" : "Credit removed by an admin",
            _ => "Credit change",
        };
        return new PlayerTimelineEventDto
        {
            Id = $"credit:{c.Id}",
            Type = T.Credit,
            Subtype = Camel(c.Kind.ToString()),
            OccurredAt = Utc(c.CreatedAt),
            Title = title,
            Detail = Join(string.IsNullOrWhiteSpace(c.Note) ? null : $"note: {c.Note.Trim()}"),
            Amount = c.Amount,
            RelatedId = c.PaymentId,
            ActorName = c.Kind == AccountCreditKind.ManualAdjustment && c.CreatedByUserId is Guid by ? names.GetValueOrDefault(by) : null,
        };
    }

    private static IEnumerable<PlayerTimelineEventDto> ReferralEvents(ReferralRedemption r, Guid userId,
        IReadOnlyDictionary<Guid, string> names, IReadOnlyDictionary<Guid, string> codes, IReadOnlyDictionary<Guid, decimal> rewards)
    {
        var code = codes.GetValueOrDefault(r.ReferralCodeId);
        var codeText = code != null ? $"code {code}" : null;
        var status = $"reward {r.RewardStatus.ToString().ToLowerInvariant()}";
        decimal? reward = rewards.TryGetValue(r.Id, out var amount) ? amount : null;

        if (r.ReferrerUserId == userId)
        {
            yield return new PlayerTimelineEventDto
            {
                Id = $"referral:{r.Id}:referrer",
                Type = T.Referral,
                Subtype = "referrer",
                OccurredAt = Utc(r.RedeemedOn),
                Title = $"Referred {names.GetValueOrDefault(r.RefereeUserId) ?? "a player"}",
                Detail = Join(codeText, status, reward is decimal granted ? $"credit {Money(granted)}" : null),
                Amount = reward,
            };
        }
        if (r.RefereeUserId == userId)
        {
            var referrer = names.GetValueOrDefault(r.ReferrerUserId);
            yield return new PlayerTimelineEventDto
            {
                Id = $"referral:{r.Id}:referee",
                Type = T.Referral,
                Subtype = "referee",
                OccurredAt = Utc(r.RedeemedOn),
                Title = referrer != null ? $"Joined with {referrer}'s referral code" : "Joined with a referral code",
                Detail = Join(codeText, $"referrer {status}"),
            };
        }
    }

    private static PlayerTimelineEventDto WaiverEvent(WaiverAcceptance w) => new()
    {
        Id = $"waiver:{w.Id}",
        Type = T.Waiver,
        Subtype = "accepted",
        OccurredAt = Utc(w.AcceptedAt),
        Title = $"Accepted waiver version {w.WaiverVersion}",
        Detail = $"version {w.WaiverVersion}",
    };

    private static IEnumerable<PlayerTimelineEventDto> AttendanceEvents(SessionAttendance a)
    {
        var session = a.Session != null ? $"session {SessionLabel(a.Session)}" : null;
        var answeredAt = a.LastUpdated < PlayerTimelineTimes.MissingBefore ? a.CreatedOn : a.LastUpdated;
        // Saving the first answer stamps CreatedOn and LastUpdated a moment apart; a later change is a real update.
        var updated = a.LastUpdated >= PlayerTimelineTimes.MissingBefore && a.LastUpdated - a.CreatedOn > TimeSpan.FromMinutes(1);
        var answer = a.IsAttending ? "coming" : "not coming";
        var outcome = AttendanceOutcome?.GetValue(a)?.ToString();

        yield return new PlayerTimelineEventDto
        {
            Id = $"attendance:{a.Id}:answer",
            Type = T.Attendance,
            Subtype = updated ? "answerUpdated" : "answer",
            OccurredAt = Utc(answeredAt),
            Title = updated ? $"Changed answer: {answer}" : $"Answered: {answer}",
            Detail = Join(session,
                string.IsNullOrWhiteSpace(a.UpdateReason) ? null : $"reason: {a.UpdateReason.Trim()}",
                string.IsNullOrWhiteSpace(a.Notes) ? null : $"notes: {a.Notes.Trim()}",
                string.IsNullOrWhiteSpace(outcome) ? null : $"outcome: {outcome}"),
            RelatedId = a.SessionId,
        };

        if (a.CheckInTime is DateTime checkedIn)
        {
            yield return new PlayerTimelineEventDto
            {
                Id = $"attendance:{a.Id}:checkin",
                Type = T.Attendance,
                Subtype = "checkIn",
                OccurredAt = Utc(checkedIn),
                Title = "Checked in",
                Detail = Join(session),
                RelatedId = a.SessionId,
            };
        }
    }

    private static PlayerTimelineEventDto MessageEvent(EmailLog m, IReadOnlyDictionary<Guid, string> names) => new()
    {
        Id = $"message:{m.Id}",
        Type = T.Message,
        Subtype = Camel(m.Status.ToString()),
        OccurredAt = Utc(m.SentAt),
        Title = m.Subject,
        Detail = Join(m.EmailType.ToString(), m.Status.ToString().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(m.ErrorMessage) ? null : $"error: {m.ErrorMessage.Trim()}"),
        ActorName = m.SentByUserId is Guid by ? names.GetValueOrDefault(by) : null,
    };

    private static PlayerTimelineEventDto NotificationEvent(Notification n)
    {
        var body = (n.Body ?? "").Trim();
        if (body.Length > MaxBodyPreview) body = body[..MaxBodyPreview].TrimEnd() + "…";
        return new PlayerTimelineEventDto
        {
            Id = $"notification:{n.Id}",
            Type = T.Notification,
            Subtype = n.ReadAt != null ? "read" : "unread",
            OccurredAt = Utc(n.CreatedOn),
            Title = n.Title,
            Detail = Join(n.ReadAt is DateTime read ? $"read {Utc(read):yyyy-MM-dd HH:mm} UTC" : "unread", body.Length > 0 ? body : null),
        };
    }

    private static PlayerTimelineEventDto AdminEvent(AuditLog l) => new()
    {
        Id = $"admin:{l.Id}",
        Type = T.Admin,
        Subtype = l.Action,
        OccurredAt = Utc(l.CreatedAt),
        Title = l.Action,
        Detail = l.Details?.Trim() ?? "",
        ActorName = string.IsNullOrWhiteSpace(l.UserName) ? null : l.UserName,
    };

    private static string SessionLabel(Session s)
    {
        var label = $"{s.SessionDate:yyyy-MM-dd} {s.StartTime}".Trim();
        return string.IsNullOrWhiteSpace(s.Location) ? label : $"{label} at {s.Location}";
    }

    private static string Join(params string?[] parts) => string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture) + " $";

    private static string Camel(string name) => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];

    // Stored in UTC; SQL Server returns it without a kind, so mark it before it serializes.
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

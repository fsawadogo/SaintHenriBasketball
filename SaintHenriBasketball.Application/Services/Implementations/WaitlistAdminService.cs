using System.Globalization;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.WaitlistAdmin;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

/// Reads come from IWaitlistAdminRepository. Offers, removals and moves reuse the waitlist machinery: they run under
/// the session-row lock in IParticipationRepository, and offers notify through IWaitlistService like automatic promotion.
public class WaitlistAdminService(
    IWaitlistAdminRepository repository,
    IWaitlistService waitlist,
    IParticipationRepository participation,
    ICacheService cache,
    ILogger<WaitlistAdminService> logger) : IWaitlistAdminService
{
    public const int DefaultWeeks = 8;
    public const int MaxWeeks = 52;

    public async Task<WaitlistSessionDto> GetSessionWaitlistAsync(Guid sessionId)
    {
        var snapshot = await repository.GetSessionWaitlistAsync(sessionId, DateTime.UtcNow)
            ?? throw new NotFoundException("Session not found");
        var offers = snapshot.Entries.Count(e => e.Entry.Status == WaitlistStatus.Offered);
        return new WaitlistSessionDto
        {
            Session = Header(snapshot.Session, snapshot.Occupied, offers),
            Entries = snapshot.Entries.Select((e, i) => new WaitlistAdminEntryDto
            {
                EntryId = e.Entry.Id,
                PlayerId = e.Entry.UserId,
                Name = e.Entry.User == null ? "Unknown" : $"{e.Entry.User.FirstName} {e.Entry.User.LastName}".Trim(),
                Email = e.Entry.User?.Email,
                Position = i + 1,
                Status = e.Entry.Status.ToString(),
                JoinedAt = Utc(e.Entry.RegistrationDate),
                OfferSentAt = Utc(e.OfferSentAt),
                OfferExpiresAt = e.Entry.Status == WaitlistStatus.Offered ? Utc(e.Entry.OfferExpiresAt) : null,
            }).ToList(),
        };
    }

    public async Task<WaitlistDemandDto> GetDemandAsync(int weeks = DefaultWeeks)
    {
        weeks = Math.Clamp(weeks, 1, MaxWeeks);
        var today = SessionTimeHelper.ToLocal(DateTime.UtcNow).Date;
        var end = today.AddDays(7 * weeks);
        var rows = await repository.GetDemandAsync(today, end, DateTime.UtcNow);
        return new WaitlistDemandDto
        {
            From = Day(today),
            To = Day(end.AddDays(-1)),
            Weeks = weeks,
            Sessions = rows.Select(r =>
            {
                var header = Header(r.Session, r.Occupied, r.OffersOutstanding);
                return new WaitlistDemandSessionDto
                {
                    SessionId = header.SessionId,
                    SessionDate = header.SessionDate,
                    StartTime = header.StartTime,
                    EndTime = header.EndTime,
                    Location = header.Location,
                    Status = header.Status,
                    StartsAt = header.StartsAt,
                    Capacity = header.Capacity,
                    Registered = r.Occupied,
                    Waitlisted = r.Waiting,
                    OffersOutstanding = r.OffersOutstanding,
                    OpenSpots = header.OpenSpots,
                    FillRate = r.Session.MaxCapacity <= 0 ? 0 : Math.Round((double)r.Occupied / r.Session.MaxCapacity, 4),
                };
            }).ToList(),
        };
    }

    public async Task<WaitlistOfferResultDto> SendOfferAsync(Guid entryId)
    {
        var outcome = await waitlist.OfferEntryAsync(entryId);
        await SessionCacheKeys.InvalidateAsync(cache, outcome.SessionId);
        var snapshot = await GetSessionWaitlistAsync(outcome.SessionId);
        var entry = snapshot.Entries.SingleOrDefault(e => e.EntryId == entryId)
            ?? throw new ValidationException("The offer ended immediately because the session is starting.");
        var header = snapshot.Session;
        var others = Math.Max(0, header.OffersOutstanding - 1);
        var deadline = SessionTimeHelper.ToLocal(outcome.OfferExpiresAt).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        string message;
        if (outcome.HadOpenSpot)
        {
            message = $"Offer sent to {entry.Name}. They have until {deadline} (Montreal) to book the place, the same as an automatic offer.";
        }
        else
        {
            // Booking (ParticipationRepository.ReserveCoreAsync) lets an offer holder in only while
            // bookings + offers held by others < capacity, so an offer on a full session can never overbook.
            message = $"Offer sent to {entry.Name}, but the session has no open spot ({header.RegisteredCount} of {header.Capacity} places booked, {others} other offer(s) outstanding). "
                + $"Accepting only works if bookings plus other outstanding offers drop below capacity before {deadline} (Montreal); until then the booking is refused with \"No places are available\", so the session is never overbooked. "
                + "While this offer is outstanding it holds a place: the next place that opens is kept for this player and is not offered to or bookable by anyone else.";
            if (others > 0)
                message += $" Offers count against each other: the {others} other offered player(s) now need a place to open too, or an offer to expire, before they can book.";
        }

        return new WaitlistOfferResultDto { Entry = entry, Session = header, HadOpenSpot = outcome.HadOpenSpot, OtherOffersOutstanding = others, Message = message };
    }

    public async Task RemoveEntryAsync(Guid entryId)
    {
        // No email or notification to the removed player.
        var removed = await participation.RemoveWaitlistEntryAsync(entryId);
        await SessionCacheKeys.InvalidateAsync(cache, removed.SessionId);
        if (removed.Status != WaitlistStatus.Offered) return;
        // A removed offer released the place it held: offer it to the next player, as when a player leaves the list.
        try { await waitlist.PromoteNextAsync(removed.SessionId); }
        catch (Exception ex) { logger.LogWarning(ex, "Waitlist promotion after admin removal failed for session {SessionId}", removed.SessionId); }
    }

    public async Task<WaitlistSessionDto> MoveEntryAsync(Guid entryId, int position)
    {
        var (sessionId, _) = await participation.MoveWaitlistEntryAsync(entryId, position);
        return await GetSessionWaitlistAsync(sessionId);
    }

    private static WaitlistSessionHeaderDto Header(Session session, int occupied, int offers) => new()
    {
        SessionId = session.Id,
        SessionDate = Day(session.SessionDate),
        StartTime = session.StartTime,
        EndTime = session.EndTime,
        Location = session.Location,
        Status = session.Status.ToString(),
        StartsAt = SessionTimeHelper.ToUtc(SessionTimeHelper.CombineLocal(session.SessionDate, session.StartTime)),
        Capacity = session.MaxCapacity,
        RegisteredCount = occupied,
        OffersOutstanding = offers,
        OpenSpots = Math.Max(0, session.MaxCapacity - occupied - offers),
    };

    private static string Day(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? Utc(DateTime? value) => value is DateTime v ? Utc(v) : null;
}

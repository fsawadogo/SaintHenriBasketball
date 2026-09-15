using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.DTOs.Notifications;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Regression checks for the `waitlist-admin` feature. Uses its own data; other checks share the database.
internal static class WaitlistAdminChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AppUrl"] = "http://localhost" }).Build();
        var notifications = new WaitlistAdminFakeNotifications();
        var email = DispatchProxy.Create<IEmailService, WaitlistAdminFakeEmail>();
        var emailed = ((WaitlistAdminFakeEmail)(object)email).Recipients;
        var cache = new WaitlistAdminFakeCache();

        WaitlistAdminService Admin(ApplicationDbContext c) => new(new WaitlistAdminRepository(c),
            new WaitlistService(new ParticipationRepository(c), config, notifications, null!, new WaitlistRepository(c), new SessionRepository(c),
                new UserRepository(c, NullLogger<UserRepository>.Instance), email, NullLogger<WaitlistService>.Instance),
            new ParticipationRepository(c), cache, NullLogger<WaitlistAdminService>.Instance);
        async Task<T> With<T>(Func<WaitlistAdminService, Task<T>> action) { await using var c = db(); return await action(Admin(c)); }
        async Task<string?> Refusal(Func<WaitlistAdminService, Task> action)
        {
            try { await using var c = db(); await action(Admin(c)); return null; }
            catch (ValidationException ex) { return "400 " + ex.Message; }
            catch (NotFoundException ex) { return "404 " + ex.Message; }
        }
        async Task<List<(Guid UserId, int Position)>> StoredLineAsync(Guid sessionId)
        {
            await using var c = db();
            return (await c.Waitlists.AsNoTracking()
                .Where(w => w.SessionId == sessionId && (w.Status == WaitlistStatus.Waiting || w.Status == WaitlistStatus.Offered))
                .OrderBy(w => w.Position).Select(w => new { w.UserId, w.Position }).ToListAsync())
                .Select(x => (x.UserId, x.Position)).ToList();
        }
        static bool Contiguous(List<(Guid UserId, int Position)> line) => line.Select(l => l.Position).SequenceEqual(Enumerable.Range(1, line.Count));
        async Task<Guid> EntryIdAsync(Guid sessionId, Guid userId)
        {
            await using var c = db();
            return await c.Waitlists.Where(w => w.SessionId == sessionId && w.UserId == userId).Select(w => w.Id).SingleAsync();
        }
        async Task<List<Guid>> MoveAsync(Guid sessionId, Guid userId, int position)
        {
            var entryId = await EntryIdAsync(sessionId, userId);
            return (await With(a => a.MoveEntryAsync(entryId, position))).Entries.Select(e => e.PlayerId).ToList();
        }

        // A full session (3 of 3 booked, 5 waiting), a session with a free place, a cancelled one and one beyond 8 weeks.
        var localToday = SessionTimeHelper.ToLocal(DateTime.UtcNow).Date;
        var players = Enumerable.Range(0, 10).Select(i => new ApplicationUser($"wla{tag}{i}", $"wla{tag}{i}@example.test", "test-only", "Waitlist", $"Player{i}", PaymentPlan.DropIn) { EmailConfirmed = true }).ToArray();
        var full = new Session(localToday.AddDays(10), 3, 10, "10:00", "12:00", $"Waitlist admin full {tag}");
        var open = new Session(localToday.AddDays(17), 2, 10, "10:00", "12:00", $"Waitlist admin open {tag}");
        var cancelled = new Session(localToday.AddDays(24), 5, 10, "10:00", "12:00", $"Waitlist admin cancelled {tag}");
        var later = new Session(localToday.AddDays(70), 5, 10, "10:00", "12:00", $"Waitlist admin later {tag}");
        var booked = players[..3];
        var waiting = players[3..8];
        await using (var c = db()) { c.Users.AddRange(players); c.Sessions.AddRange(full, open, cancelled, later); await c.SaveChangesAsync(); }
        await using (var c = db())
        {
            var repo = new ParticipationRepository(c);
            foreach (var p in booked) await repo.ReserveAsync(full.Id, p.Id);
            foreach (var p in waiting) await repo.JoinWaitlistAsync(full.Id, p.Id, null);
            await repo.ReserveAsync(open.Id, players[8].Id);
            await repo.JoinWaitlistAsync(open.Id, players[9].Id, null);
            await repo.JoinWaitlistAsync(cancelled.Id, players[9].Id, null);
            (await c.Sessions.SingleAsync(s => s.Id == cancelled.Id)).Status = SessionStatus.Cancelled;
            await c.SaveChangesAsync();
        }

        // Listing
        var list = await With(a => a.GetSessionWaitlistAsync(full.Id));
        assert(list.Entries.Select(e => e.PlayerId).SequenceEqual(waiting.Select(p => p.Id)) && list.Entries.Select(e => e.Position).SequenceEqual(new[] { 1, 2, 3, 4, 5 })
            && list.Entries.All(e => e.Status == "Waiting" && e.OfferSentAt == null && e.OfferExpiresAt == null && e.JoinedAt.Kind == DateTimeKind.Utc)
            && list.Entries[0].Name == "Waitlist Player3" && list.Entries[0].Email == waiting[0].Email
            && list.Session is { Capacity: 3, RegisteredCount: 3, OffersOutstanding: 0, OpenSpots: 0, StartTime: "10:00", EndTime: "12:00", Status: "Full" }
            && list.Session.SessionDate == full.SessionDate.ToString("yyyy-MM-dd") && list.Session.StartsAt.Kind == DateTimeKind.Utc,
            "admin waitlist lists waiting players in position order with the session's capacity, bookings and open spots");
        assert((await Refusal(a => a.GetSessionWaitlistAsync(Guid.NewGuid())))?.StartsWith("404") == true, "admin waitlist for an unknown session is not found");

        // Demand
        var demand = await With(a => a.GetDemandAsync());
        var longer = await With(a => a.GetDemandAsync(12));
        assert(demand.Weeks == 8
            && demand.Sessions.SingleOrDefault(r => r.SessionId == full.Id) is { Capacity: 3, Registered: 3, Waitlisted: 5, OffersOutstanding: 0, OpenSpots: 0, FillRate: 1.0 }
            && demand.Sessions.SingleOrDefault(r => r.SessionId == open.Id) is { Capacity: 2, Registered: 1, Waitlisted: 1, OffersOutstanding: 0, OpenSpots: 1, FillRate: 0.5 }
            && demand.Sessions.All(r => r.SessionId != cancelled.Id && r.SessionId != later.Id)
            && longer.Sessions.Any(r => r.SessionId == later.Id) && longer.Sessions.All(r => r.SessionId != cancelled.Id),
            "waitlist demand covers the next 8 weeks by default without cancelled sessions, with bookings, waitlist, offers and fill rate");

        // Offer to a specific entry: with a free place, then on a full session.
        var openEntryId = await EntryIdAsync(open.Id, players[9].Id);
        var openOffer = await With(a => a.SendOfferAsync(openEntryId));
        var openDemand = (await With(a => a.GetDemandAsync())).Sessions.Single(r => r.SessionId == open.Id);
        assert(openOffer is { HadOpenSpot: true, OtherOffersOutstanding: 0, Entry.Status: "Offered", Session.OpenSpots: 0, Session.OffersOutstanding: 1 }
            && openDemand is { OffersOutstanding: 1, Waitlisted: 0, OpenSpots: 0 },
            "an admin offer on a session with a free place holds it and counts as an outstanding offer");

        var target = list.Entries[2];
        var before = DateTime.UtcNow;
        var fullOffer = await With(a => a.SendOfferAsync(target.EntryId));
        assert(fullOffer is { HadOpenSpot: false, OtherOffersOutstanding: 0, Entry: { Status: "Offered", Position: 3 }, Session.OpenSpots: 0 }
            && fullOffer.Message.Contains("no open spot") && fullOffer.Message.Contains("never overbooked")
            && fullOffer.Entry.OfferExpiresAt is DateTime expires && expires.Kind == DateTimeKind.Utc && Math.Abs((expires - before.AddHours(2)).TotalMinutes) < 1
            && fullOffer.Entry.OfferSentAt is DateTime sentAt && sentAt.Kind == DateTimeKind.Utc && Math.Abs((sentAt - before).TotalMinutes) < 1,
            "an admin offer on a full session is still sent, says there is no open spot and uses the normal two-hour expiry");
        assert(notifications.Sent.Contains((waiting[2].Id, NotificationType.WaitlistOffer)) && emailed.Contains(waiting[2].Email!)
            && notifications.Sent.Contains((players[9].Id, NotificationType.WaitlistOffer)) && emailed.Contains(players[9].Email!),
            "an admin offer sends the same in-app notification and email as automatic promotion");

        var cancelledEntryId = await EntryIdAsync(cancelled.Id, players[9].Id);
        var offeredAgain = await Refusal(a => a.SendOfferAsync(target.EntryId));
        var unknownEntry = await Refusal(a => a.SendOfferAsync(Guid.NewGuid()));
        var cancelledSession = await Refusal(a => a.SendOfferAsync(cancelledEntryId));
        assert(offeredAgain?.StartsWith("400") == true && unknownEntry?.StartsWith("404") == true && cancelledSession?.StartsWith("400") == true,
            "an admin offer is refused for an entry that already has an offer, an unknown entry and a cancelled session");

        // Accepting an offer made while full: refused until a place opens, then it books without overbooking.
        await using (var c = db())
        {
            var repo = new ParticipationRepository(c);
            var refusedWhileFull = false;
            try { await repo.ReserveAsync(full.Id, waiting[2].Id); }
            catch (ValidationException) { refusedWhileFull = true; }
            await repo.CancelAsync(full.Id, booked[0].Id);
            var automaticOffer = await repo.OfferNextAsync(full.Id);
            var outsiderRefused = false;
            try { await repo.ReserveAsync(full.Id, players[8].Id); }
            catch (ValidationException) { outsiderRefused = true; }
            await repo.ReserveAsync(full.Id, waiting[2].Id);
            var afterAccept = await c.Sessions.AsNoTracking().SingleAsync(s => s.Id == full.Id);
            var acceptedEntry = await c.Waitlists.AsNoTracking().SingleAsync(w => w.Id == target.EntryId);
            assert(refusedWhileFull && automaticOffer == null && outsiderRefused && afterAccept.RegisteredPlayersCount == 3 && acceptedEntry.Status == WaitlistStatus.Accepted,
                "accepting an offer sent while full is refused until a place opens; the opening is held for that player and never overbooks");
        }
        var acceptedOffer = await Refusal(a => a.SendOfferAsync(target.EntryId));
        var acceptedRemove = await Refusal(a => a.RemoveEntryAsync(target.EntryId));
        var acceptedMove = await Refusal(a => a.MoveEntryAsync(target.EntryId, 1));
        assert(acceptedOffer?.StartsWith("400") == true && acceptedRemove?.StartsWith("400") == true && acceptedMove?.StartsWith("400") == true,
            "an entry that is no longer waiting can't be offered, removed or moved");

        // Remove with renumbering; nobody is emailed.
        var emailsBefore = emailed.Count;
        var notificationsBefore = notifications.Sent.Count;
        var removedId = list.Entries[1].EntryId;
        await With(async a => { await a.RemoveEntryAsync(removedId); return true; });
        var afterRemove = await With(a => a.GetSessionWaitlistAsync(full.Id));
        var storedAfterRemove = await StoredLineAsync(full.Id);
        bool removedRowGone;
        await using (var c = db()) removedRowGone = !await c.Waitlists.AnyAsync(w => w.Id == removedId);
        assert(afterRemove.Entries.Select(e => e.PlayerId).SequenceEqual(new[] { waiting[0].Id, waiting[3].Id, waiting[4].Id })
            && afterRemove.Entries.Select(e => e.Position).SequenceEqual(new[] { 1, 2, 3 }) && Contiguous(storedAfterRemove) && storedAfterRemove.Count == 3
            && removedRowGone && emailed.Count == emailsBefore && notifications.Sent.Count == notificationsBefore
            && (await Refusal(a => a.RemoveEntryAsync(removedId)))?.StartsWith("404") == true,
            "removing a waitlist entry deletes it, renumbers the rest without gaps and emails no one");
        await using (var c = db()) await new ParticipationRepository(c).JoinWaitlistAsync(full.Id, booked[0].Id, null);
        var storedAfterJoin = await StoredLineAsync(full.Id);
        assert(Contiguous(storedAfterJoin) && storedAfterJoin.Count == 4 && storedAfterJoin[3].UserId == booked[0].Id,
            "a player joining after an admin change lands right after the last position");

        // Reorder with clamping.
        var (w0, w3, w4, joiner) = (waiting[0].Id, waiting[3].Id, waiting[4].Id, booked[0].Id);
        var toFront = await MoveAsync(full.Id, joiner, 1);
        var pastEnd = await MoveAsync(full.Id, w0, 99);
        var belowOne = await MoveAsync(full.Id, w4, -3);
        var samePlace = await MoveAsync(full.Id, w3, 3);
        assert(toFront.SequenceEqual(new[] { joiner, w0, w3, w4 }) && pastEnd.SequenceEqual(new[] { joiner, w3, w4, w0 })
            && belowOne.SequenceEqual(new[] { w4, joiner, w3, w0 }) && samePlace.SequenceEqual(new[] { w4, joiner, w3, w0 })
            && Contiguous(await StoredLineAsync(full.Id)) && (await Refusal(a => a.MoveEntryAsync(Guid.NewGuid(), 1)))?.StartsWith("404") == true,
            "moving a waitlist entry shifts the others and clamps the target to the line");

        // Concurrent reorders by several admins.
        var entryIds = new List<Guid>();
        foreach (var userId in new[] { w4, joiner, w3, w0 }) entryIds.Add(await EntryIdAsync(full.Id, userId));
        await Task.WhenAll(Enumerable.Range(0, 12).Select(async i =>
        {
            await using var c = db();
            await Admin(c).MoveEntryAsync(entryIds[i % entryIds.Count], (i * 3) % 6 - 1);
        }));
        var storedAfterRace = await StoredLineAsync(full.Id);
        assert(Contiguous(storedAfterRace) && storedAfterRace.Select(l => l.UserId).OrderBy(id => id).SequenceEqual(new[] { w4, joiner, w3, w0 }.OrderBy(id => id)),
            "concurrent waitlist reorders keep positions contiguous with no player lost or duplicated");
    }
}

/// Records SendEmailAsync; any other email method is unexpected here.
public class WaitlistAdminFakeEmail : DispatchProxy
{
    public List<string> Recipients { get; } = new();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == nameof(IEmailService.SendEmailAsync))
        {
            lock (Recipients) Recipients.Add((string)args![0]!);
            return Task.CompletedTask;
        }
        throw new NotSupportedException($"Unexpected email: {targetMethod?.Name}");
    }
}

internal sealed class WaitlistAdminFakeNotifications : INotificationService
{
    public List<(Guid UserId, NotificationType Type)> Sent { get; } = new();
    public Task CreateAsync(Guid userId, NotificationType type, string title, string body, string? url = null) { lock (Sent) Sent.Add((userId, type)); return Task.CompletedTask; }
    public Task<IReadOnlyList<NotificationDto>> GetRecentAsync(Guid userId, int take = 20) => Task.FromResult<IReadOnlyList<NotificationDto>>(Array.Empty<NotificationDto>());
    public Task<int> GetUnreadCountAsync(Guid userId) => Task.FromResult(0);
    public Task<bool> MarkAsReadAsync(Guid userId, Guid notificationId) => Task.FromResult(false);
    public Task<int> MarkAllAsReadAsync(Guid userId) => Task.FromResult(0);
}

internal sealed class WaitlistAdminFakeCache : ICacheService
{
    public Task<T?> GetAsync<T>(string key) => Task.FromResult<T?>(default);
    public Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null) => Task.CompletedTask;
    public Task RemoveAsync(string key) => Task.CompletedTask;
    public Task RemoveByPrefixAsync(string prefix) => Task.CompletedTask;
}

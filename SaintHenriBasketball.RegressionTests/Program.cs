using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

var database = args.Contains("--seed-preview") ? "SHB_LocalPreview" : "SHB_Regression_" + Guid.NewGuid().ToString("N");
var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True").Options;
ApplicationDbContext Db() => new(options);
if (args.Contains("--seed-preview"))
{
    await using var preview = Db();
    var player = await preview.Users.SingleAsync(u => u.Username == "localplayer" && u.Email == "localplayer@example.test");
    if (!await preview.Users.AnyAsync(u => u.Username == "localadmin"))
    {
        preview.Users.Add(new ApplicationUser("localadmin", "localadmin@example.test", player.PasswordHash, "Admin", "Local", PaymentPlan.DropIn) { EmailConfirmed = true, IsAdmin = true });
        await preview.SaveChangesAsync();
    }
    var previewSession = Guid.Parse("fd9d3800-be17-4d95-8022-69cc914cd7e2");
    if (!await preview.Payments.AnyAsync(p => p.UserId == player.Id && p.SessionId == previewSession))
    {
        preview.Payments.Add(new Payment(player.Id, 10, PaymentPlan.DropIn, previewSession) { Reference = "LOCAL-PREVIEW-NO-CHARGE", CreatedAt = DateTime.UtcNow });
        await preview.SaveChangesAsync();
    }
    Console.WriteLine("Local preview admin ready: localadmin (same test password as localplayer)");
    return;
}
if (args.Contains("--migrations"))
{
    await using var migrationDb = Db();
    await migrationDb.Database.MigrateAsync();
    await migrationDb.Sessions.FirstOrDefaultAsync();
    await migrationDb.Users.FirstOrDefaultAsync();
    Console.WriteLine("PASS: fresh database migrations and current model queries");
    return;
}
void Assert(bool result, string name) { if (!result) throw new Exception("FAILED: " + name); Console.WriteLine("PASS: " + name); }
var players = Enumerable.Range(0, 12).Select(i => new ApplicationUser($"test{i}", $"test{i}@example.test", "test-only", "Local", "Test", PaymentPlan.DropIn) { EmailConfirmed = true }).ToArray();
var session = new Session(DateTime.UtcNow.Date.AddDays(3), 1, 10, "10:00", "12:00", "Regression court");
await using (var db = Db()) { await db.Database.EnsureCreatedAsync(); db.Users.AddRange(players); db.Sessions.Add(session); await db.SaveChangesAsync(); }
var results = await Task.WhenAll(players.Select(async user => {
    await using var db = Db();
    try { await new ParticipationRepository(db).ReserveAsync(session.Id, user.Id); return user.Id; }
    catch (ValidationException) { return Guid.Empty; }
}));
var winner = results.Single(id => id != Guid.Empty);
Assert(results.Count(id => id != Guid.Empty) == 1, "concurrent requests cannot overbook the last place");
await using (var db = Db()) {
    var repo = new ParticipationRepository(db);
    await repo.ReserveAsync(session.Id, winner);
    var rsvp = await repo.SetAttendanceAsync(session.Id, winner, true, null, null);
    Assert(rsvp.CheckInTime == null, "attendance confirmation does not claim physical check-in");
    Assert((await db.Sessions.SingleAsync()).RegisteredPlayersCount == 1, "reservation plus RSVP occupies one place");
    await repo.CancelAsync(session.Id, winner); await repo.CancelAsync(session.Id, winner);
    Assert((await db.Sessions.SingleAsync()).RegisteredPlayersCount == 0, "repeat cancellation is safe and releases one place");
}
var waiting = players.Where(p => p.Id != winner).Take(3).ToArray();
await using (var db = Db()) {
    var repo = new ParticipationRepository(db);
    foreach (var player in waiting) await repo.JoinWaitlistAsync(session.Id, player.Id, null);
    try { await repo.ReserveAsync(session.Id, winner); Assert(false, "waitlist priority protected"); }
    catch (ValidationException) { Assert(true, "new bookings cannot jump a waiting player before promotion"); }
    var offer = await repo.OfferNextAsync(session.Id);
    Assert(offer?.UserId == waiting[0].Id && offer.OfferExpiresAt > DateTime.UtcNow, "first waiting player receives a timed offer");
    Assert(await repo.OfferNextAsync(session.Id) == null, "an offered place is not offered twice");
}
await using (var db = Db()) {
    try { await new ParticipationRepository(db).ReserveAsync(session.Id, winner); Assert(false, "reserved offer protected"); }
    catch (ValidationException) { Assert(true, "other players cannot take an offered place"); }
}
await using (var db = Db()) { var offer = await db.Waitlists.SingleAsync(w => w.Status == WaitlistStatus.Offered); offer.OfferExpiresAt = DateTime.UtcNow.AddMinutes(-1); await db.SaveChangesAsync(); }
await using (var db = Db()) {
    var repo = new ParticipationRepository(db);
    var next = await repo.OfferNextAsync(session.Id);
    Assert(next?.UserId == waiting[1].Id, "expired offer advances to next player");
    await repo.ReserveAsync(session.Id, waiting[1].Id);
    Assert((await db.Waitlists.SingleAsync(w => w.UserId == waiting[1].Id)).Status == WaitlistStatus.Accepted, "claim converts offer into reservation");
}
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["JwtSettings:Key"] = "local-regression-key-not-used-by-the-running-app", ["AppUrl"] = "http://localhost" }).Build();
var links = new AttendanceLinks(config);
var token = links.Create(session.Id, winner, false, DateTime.UtcNow.AddHours(1)).Split("token=")[1];
Assert(links.Validate(token)?.Attending == false, "signed cancel link preserves action");
Assert(links.Validate(token.Replace(".0.", ".1.")) == null, "tampered reminder action is rejected");
Assert(links.Validate(links.Create(session.Id, winner, true, DateTime.UtcNow.AddMinutes(-1)).Split("token=")[1]) == null, "expired reminder is rejected");
await using (var db = Db()) {
    var repo = new ParticipationRepository(db);
    try { await repo.CheckInAsync(session.Id, waiting[1].Id); Assert(false, "early check-in rejected"); }
    catch (ValidationException) { Assert(true, "future session cannot be checked in early"); }
    await repo.CancelAsync(session.Id, waiting[1].Id);
    var offered = await repo.OfferNextAsync(session.Id);
    Assert(offered?.UserId == waiting[2].Id, "next eligible player receives released place");
    await repo.LeaveWaitlistAsync(session.Id, waiting[2].Id);
    await repo.ReserveAsync(session.Id, winner);
    Assert((await db.Waitlists.SingleAsync(w => w.UserId == waiting[2].Id)).Status == WaitlistStatus.Cancelled, "leaving releases offered place");
    var current = await db.Sessions.SingleAsync();
    var local = SessionTimeHelper.ToLocal(DateTime.UtcNow);
    current.SessionDate = local.Date;
    current.StartTime = local.AddMinutes(10).ToString("HH:mm");
    current.EndTime = local.AddMinutes(20).ToString("HH:mm");
    await db.SaveChangesAsync();
    var first = (await repo.CheckInAsync(session.Id, winner)).CheckInTime;
    var repeated = (await repo.CheckInAsync(session.Id, winner)).CheckInTime;
    Assert(first != null && repeated == first, "repeated check-in preserves original arrival time");
    Assert(current.RegisteredPlayersCount == 1, "check-in does not double-count registration");
    try { await repo.CheckInAsync(session.Id, waiting[0].Id); Assert(false, "full walk-in rejected"); }
    catch (ValidationException) { Assert(true, "walk-in cannot overbook a full session"); }
}
var paymentResults = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ => {
    await using var db = Db();
    return await new PaymentRepository(db).GetOrCreateSessionPaymentAsync(winner, session.Id, 10m);
}));
Assert(paymentResults.Count(r => r.Created) == 1 && paymentResults.Select(r => r.Payment.Id).Distinct().Count() == 1,
    "concurrent payment submissions reuse one session payment");
await using (var db = Db()) {
    var record = await db.Payments.SingleAsync();
    Assert(record.SessionId == session.Id && record.UserId == winner && record.Status == PaymentStatus.Pending,
        "session payment is associated with its owner and awaits verification");
}
await using (var db = Db()) {
    var record = await db.Payments.SingleAsync();
    var repo = new PaymentRepository(db);
    Assert(await repo.TrySetPendingReferenceAsync(record.Id, record.Reference, "TEST|INTERAC:LOCAL"), "pending reference can be submitted once");
    Assert(!await repo.TrySetPendingReferenceAsync(record.Id, record.Reference, "TEST|INTERAC:STALE"), "stale reference cannot overwrite a newer submission");
    await db.Payments.Where(p => p.Id == record.Id).ExecuteUpdateAsync(update => update.SetProperty(p => p.Status, PaymentStatus.Completed));
    Assert(!await repo.TrySetPendingReferenceAsync(record.Id, "TEST|INTERAC:LOCAL", "TEST|INTERAC:LATE"), "late reference cannot alter a confirmed payment");
}
await using (var db = Db()) {
    var firstSeason = new Season(new DateTime(2026, 1, 1), new DateTime(2026, 6, 30), 95) { Name = "First" };
    var nextSeason = new Season(new DateTime(2026, 9, 1), new DateTime(2026, 12, 31), 95) { Name = "Next" };
    db.Seasons.AddRange(firstSeason, nextSeason);
    db.Payments.Add(new Payment(winner, 95, PaymentPlan.Season) { SeasonId = firstSeason.Id, Status = PaymentStatus.Completed });
    db.Payments.Add(new Payment(winner, 95, PaymentPlan.Season) { SeasonId = nextSeason.Id });
    db.Payments.Add(new Payment(winner, 95, PaymentPlan.Season));
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
    var records = await new PaymentRepository(db).GetPaymentsByUserAsync(winner);
    Assert(records.Count(p => p.SeasonId == firstSeason.Id && p.Status == PaymentStatus.Completed) == 1, "completed payment retains its exact season");
    Assert(records.Count(p => p.SeasonId == nextSeason.Id && p.Status == PaymentStatus.Pending) == 1, "new season remains unpaid despite old season payment");
    Assert(records.Any(p => p.Plan == PaymentPlan.Season && p.SeasonId == null), "legacy payment remains unassigned rather than guessed");
}
Guid checkoutSeasonId;
await using (var db = Db()) {
    var checkoutSeason = new Season(new DateTime(2027, 1, 1), new DateTime(2027, 4, 30), 110) { Name = "Checkout" };
    db.Seasons.Add(checkoutSeason);
    await db.SaveChangesAsync();
    checkoutSeasonId = checkoutSeason.Id;
}
var seasonPaymentResults = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ => {
    await using var db = Db();
    return await new PaymentRepository(db).GetOrCreateSeasonPaymentAsync(winner, checkoutSeasonId, 110m);
}));
Assert(seasonPaymentResults.Count(r => r.Created) == 1 && seasonPaymentResults.Select(r => r.Payment.Id).Distinct().Count() == 1,
    "concurrent season payment submissions reuse one season payment");
Console.WriteLine($"Regression checks complete. Isolated database retained: {database}");

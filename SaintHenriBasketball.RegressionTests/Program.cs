using SaintHenriBasketball.Application.DTOs.AuditLog;
using System.Net;
using System.IdentityModel.Tokens.Jwt;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Application.DTOs.Payment;
using SaintHenriBasketball.Application.DTOs.PromoCodes;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Application.Mapping;
using SaintHenriBasketball.Application.DTOs.Broadcast;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.DTOs.SessionTemplate;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
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
    await migrationDb.Payments.Include(p => p.PromoCode).FirstOrDefaultAsync();
    await migrationDb.AccountCredits.FirstOrDefaultAsync();
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
Assert(!SmsEncoding.RequiresUnicode("SHB reminder: your session is today at 10:00. See you at the gym."),
    "English reminder fits the GSM-7 character set");
Assert(SmsEncoding.RequiresUnicode("Rappel SHB: votre séance est aujourd'hui à 10:00. À tantôt au gymnase."),
    "French reminder containing ô is sent as Unicode");
Assert(BrevoSmsService.ToRecipient("(514) 555-0142") == "15145550142"
    && BrevoSmsService.ToRecipient("+1 514 555 0142") == "15145550142"
    && BrevoSmsService.ToRecipient("555-0142") == null,
    "phone numbers normalise to Brevo recipient digits");
var brevoConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
    ["Sms:Brevo:ApiKey"] = "regression-key", ["Sms:Brevo:Sender"] = "SHB" }).Build();
var brevoAccepted = new StubSmsHandler(HttpStatusCode.Created, "{\"reference\":\"r\",\"messageId\":42,\"usedCredits\":1.5}");
var brevo = new BrevoSmsService(new HttpClient(brevoAccepted), brevoConfig, NullLogger<BrevoSmsService>.Instance);
Assert(await brevo.SendAsync("514-555-0142", "Rappel SHB: À tantôt"), "Brevo accepted send reports success");
Assert(brevoAccepted.ApiKey == "regression-key"
    && brevoAccepted.Body!.Contains("\"recipient\":\"15145550142\"")
    && brevoAccepted.Body.Contains("\"type\":\"transactional\"")
    && brevoAccepted.Body.Contains("\"unicodeEnabled\":true"),
    "Brevo request carries the key, normalised recipient, transactional type, and Unicode flag");
var brevoNoCredits = new BrevoSmsService(new HttpClient(new StubSmsHandler(HttpStatusCode.PaymentRequired, "{\"code\":\"not_enough_credits\"}")),
    brevoConfig, NullLogger<BrevoSmsService>.Instance);
Assert(!await brevoNoCredits.SendAsync("5145550142", "SHB test"), "Brevo rejection reports failure without throwing");
var brevoUnconfigured = new BrevoSmsService(new HttpClient(new StubSmsHandler(HttpStatusCode.Created, "{}")),
    new ConfigurationBuilder().Build(), NullLogger<BrevoSmsService>.Instance);
Assert(!brevoUnconfigured.IsConfigured && !await brevoUnconfigured.SendAsync("5145550142", "SHB test"),
    "unconfigured Brevo skips sending");
await using (var db = Db()) {
    var flagService = new FeatureFlagService(
        new FeatureFlagRepository(db),
        new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()), NullLogger<MemoryCacheService>.Instance),
        new AuditLogRepository(db),
        NullLogger<FeatureFlagService>.Instance);
    await flagService.SeedDefaultsAsync(new[] {
        new FeatureFlagDefinition("regression-player-flag", "Player flag", "Drapeau joueur"),
        new FeatureFlagDefinition("regression-admin-flag", "Admin flag", "Drapeau admin", IsPublic: false),
    });
    await flagService.SetEnabledAsync("regression-admin-flag", true, null, "Regression");
    var visitorFlags = await flagService.GetClientFlagsAsync(includeAdminOnly: false);
    var adminFlags = await flagService.GetClientFlagsAsync(includeAdminOnly: true);
    Assert(visitorFlags.ContainsKey("regression-player-flag") && !visitorFlags.ContainsKey("regression-admin-flag"),
        "visitors and players never receive admin-only flags");
    Assert(adminFlags.TryGetValue("regression-admin-flag", out var adminFlagEnabled) && adminFlagEnabled,
        "admins receive enabled admin-only flags");
}
await using (var db = Db()) {
    var templates = new SessionTemplateService(new SessionTemplateRepository(db), new SessionRepository(db), NullLogger<SessionTemplateService>.Instance);
    var templateDay = new DateTime(2032, 1, 1);
    while (templateDay.DayOfWeek != DayOfWeek.Saturday) templateDay = templateDay.AddDays(1);
    db.Sessions.Add(new Session(templateDay, 10, 10, "10:00:00", "12:00:00", "Regression template court"));
    await db.SaveChangesAsync();
    var template = await templates.CreateAsync(new UpsertSessionTemplateDto { DayOfWeek = DayOfWeek.Saturday, StartTime = "10:00", EndTime = "12:00", Location = "Regression template court", MaxCapacity = 10, DropInPrice = 10 });
    var generated = await templates.GenerateSessionsAsync(template.Id, templateDay, templateDay);
    Assert(generated.Created == 0 && generated.Skipped == 1, "template generation treats 10:00 and 10:00:00 as the same start time");
    foreach (var (start, end) in new[] { ("abc", "12:00"), ("12:00", "10:00") })
    {
        try { await templates.CreateAsync(new UpsertSessionTemplateDto { DayOfWeek = DayOfWeek.Saturday, StartTime = start, EndTime = end, Location = "Regression", MaxCapacity = 10, DropInPrice = 10 }); Assert(false, "invalid template time rejected"); }
        catch (ValidationException) { }
    }
    Assert(true, "templates reject unparseable times and end times before start times");
}
var qrService = new QrCheckInService(config, null!, null!, null!, null!, null!, NullLogger<QrCheckInService>.Instance);
foreach (var badToken in new[] { "garbage", "a.b.c", "" })
{
    try { await qrService.CheckInAsync(winner, badToken); Assert(false, "malformed QR token rejected"); }
    catch (ValidationException) { }
}
Assert(true, "malformed QR tokens are rejected as invalid instead of crashing");
await using (var db = Db()) {
    var interacPending = new Payment(winner, 12m, PaymentPlan.DropIn) { Reference = "DROPIN-REG|INTERAC:BANK123" };
    var plainPending = new Payment(winner, 13m, PaymentPlan.DropIn) { Reference = "LOCAL-NO-INTERAC" };
    var interacCompleted = new Payment(winner, 14m, PaymentPlan.DropIn) { Reference = "DROPIN-REG2|INTERAC:BANK456", Status = PaymentStatus.Completed };
    db.Payments.AddRange(interacPending, plainPending, interacCompleted);
    await db.SaveChangesAsync();
    var reconciliationService = new ReconciliationService(new PaymentRepository(db), null!, new AuditLogRepository(db), NullLogger<ReconciliationService>.Instance);
    var listed = await reconciliationService.GetPendingAsync();
    Assert(listed.Any(p => p.Id == interacPending.Id) && listed.All(p => p.Reference!.Contains("|INTERAC:")),
        "reconciliation lists only submitted Interac transfers");
    var bulk = await reconciliationService.BulkCompleteAsync(new[] { plainPending.Id, interacCompleted.Id }, null, "Regression");
    Assert(bulk.Completed == 0 && bulk.Skipped == 2 && bulk.Failed == 0,
        "bulk reconciliation skips payments that are not pending Interac submissions");
}
var broadcastHtml = BroadcastService.BuildEmailHtml("Club news", "Hello <b>team</b>\nSee you Saturday\n\nSecond paragraph",
    "http://localhost/unsubscribe?token=abc", EmailLanguage.English);
Assert(broadcastHtml.Contains("Hello &lt;b&gt;team&lt;/b&gt;<br/>See you Saturday") && broadcastHtml.Contains("Second paragraph") && !broadcastHtml.Contains("<b>team</b>"),
    "broadcast body is escaped and keeps its line breaks");
Assert(broadcastHtml.Contains("http://localhost/unsubscribe?token=abc") && broadcastHtml.Contains("Unsubscribe"),
    "broadcast email carries an unsubscribe link");
var unsubscribeLinks = new UnsubscribeLinks(config);
var unsubscribeToken = unsubscribeLinks.CreateToken(winner, DateTimeOffset.UtcNow.Add(UnsubscribeLinks.Lifetime));
Assert(unsubscribeLinks.Validate(unsubscribeToken)?.UserId == winner, "signed unsubscribe link identifies its user");
Assert(unsubscribeLinks.Validate(unsubscribeToken[..^1] + (unsubscribeToken[^1] == '0' ? '1' : '0')) == null, "tampered unsubscribe link is rejected");
Assert(unsubscribeLinks.Validate(unsubscribeLinks.CreateToken(winner, DateTimeOffset.UtcNow.AddMinutes(-1))) == null, "expired unsubscribe link is rejected");
await using (var db = Db()) {
    var waiverFlags = new FeatureFlagService(
        new FeatureFlagRepository(db),
        new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()), NullLogger<MemoryCacheService>.Instance),
        new AuditLogRepository(db),
        NullLogger<FeatureFlagService>.Instance);
    await waiverFlags.SeedDefaultsAsync(FeatureFlagDefinitions.All);
    var waiverRepository = new WaiverRepository(db);
    var waivers = new WaiverService(waiverRepository, null!, null!, waiverFlags, NullLogger<WaiverService>.Instance);
    var signer = players[5].Id;
    await waiverRepository.AddTemplateAsync(new WaiverTemplate(1, "Regression waiver", "Décharge de test", DateTime.UtcNow, isActive: true));
    await waivers.EnsureAcceptedAsync(signer);
    Assert(true, "waiver is not enforced while the waiver flag is off");
    await waiverFlags.SetEnabledAsync(FeatureFlagKeys.Waiver, true, null, "Regression");
    try { await waivers.EnsureAcceptedAsync(signer); Assert(false, "unaccepted waiver blocks"); }
    catch (ValidationException) { Assert(true, "an unaccepted active waiver blocks booking, waitlist, and check-in"); }
    await waivers.AcceptCurrentAsync(signer, null);
    await waivers.EnsureAcceptedAsync(signer);
    Assert(true, "accepting the current waiver lifts the block");
}
await using (var db = Db()) {
    var auditRepository = new AuditLogRepository(db);
    var tinyPage = await auditRepository.GetAllAsync(page: 0, pageSize: -5);
    var hugePage = await auditRepository.GetAllAsync(page: -1, pageSize: 100_000);
    Assert(tinyPage.Count <= 1 && hugePage.Count <= 200, "audit log paging is clamped instead of failing");
}
await using (var db = Db()) {
    var qrSession = new Session(new DateTime(2033, 6, 4), 10, 10, "10:00", "12:00", "Regression QR court");
    db.Sessions.Add(qrSession);
    await db.SaveChangesAsync();
    var qrTokens = new QrCheckInService(config, null!, null!, new SessionRepository(db), null!, null!, NullLogger<QrCheckInService>.Instance);
    var issued = await qrTokens.GenerateTokenAsync(qrSession.Id, "http://localhost");
    var expectedExpiry = SessionTimeHelper.ToUtc(SessionTimeHelper.CombineLocal(qrSession.SessionDate, "12:00")).AddMinutes(30);
    var tokenExpiry = new JwtSecurityTokenHandler().ReadJwtToken(issued.Token).ValidTo;
    Assert(Math.Abs((tokenExpiry - expectedExpiry).TotalSeconds) < 2, "QR codes printed early stay valid until 30 minutes after the session ends");
}
var broadcastQueue = new BroadcastQueue();
await broadcastQueue.EnqueueAsync(new QueuedBroadcast(new SendBroadcastRequestDto { Subject = "Queued", BodyEn = "Body" }, null, "Regression"));
using (var queueTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
{
    await foreach (var queued in broadcastQueue.ReadAllAsync(queueTimeout.Token))
    {
        Assert(queued.Request.Subject == "Queued" && queued.AdminName == "Regression", "queued broadcast reaches the background worker");
        break;
    }
}
await using (var db = Db()) {
    var acceptanceRows = await new WaiverRepository(db).GetAcceptancesAsync(1);
    Assert(acceptanceRows.Count == 1 && acceptanceRows[0].UserId == players[5].Id, "waiver acceptances are listed per version for admins");
}

// ---- Promo codes, account credits and referral rewards ----
var creditConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
    ["JwtSettings:Key"] = "local-regression-signing-key-long-enough-for-any-hmac-algorithm-0123456789abcdef",
    ["JwtSettings:Issuer"] = "regression", ["JwtSettings:Audience"] = "regression", ["JwtSettings:DurationInDays"] = "1",
    ["AppUrl"] = "http://localhost", ["Referrals:RewardAmount"] = "10.00" }).Build();
var creditMapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
FeatureFlagService CreditFlags(ApplicationDbContext db) => new(new FeatureFlagRepository(db),
    new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()), NullLogger<MemoryCacheService>.Instance),
    new AuditLogRepository(db), NullLogger<FeatureFlagService>.Instance);
NotificationService NotificationsFor(ApplicationDbContext db) =>
    new(new NotificationRepository(db), new UserRepository(db, NullLogger<UserRepository>.Instance), NullLogger<NotificationService>.Instance);
ReferralService ReferralsFor(ApplicationDbContext db) => new(new ReferralRepository(db), new UserRepository(db, NullLogger<UserRepository>.Instance),
    NullLogger<ReferralService>.Instance, new PaymentRepository(db), CreditFlags(db), NotificationsFor(db), new AuditLogRepository(db), creditConfig);
PaymentService PaymentsFor(ApplicationDbContext db) => new(new PaymentRepository(db), new UserRepository(db, NullLogger<UserRepository>.Instance),
    new SessionRepository(db), new SessionRegistrationRepository(db), creditMapper, NullLogger<PaymentService>.Instance, null!, NotificationsFor(db),
    new SeasonRepository(db, NullLogger<SeasonRepository>.Instance), new PromoCodeRepository(db), new AccountCreditRepository(db), ReferralsFor(db), CreditFlags(db),
    new SeasonPlanChoiceRepository(db, NullLogger<SeasonPlanChoiceRepository>.Instance));
UserService UsersFor(ApplicationDbContext db) => new(creditConfig, creditMapper, new UserRepository(db, NullLogger<UserRepository>.Instance), null!,
    NullLogger<UserService>.Instance, CreditFlags(db), new ReferralRepository(db));
async Task<string?> TryPayAsync(Guid userId, Guid sessionId, string? promoCode, int method = 1, string? reference = null)
{
    await using var db = Db();
    try
    {
        await PaymentsFor(db).CreateDropInPaymentAsync(userId, new CreateDropInPaymentDto { SessionId = sessionId, PaymentMethod = method, PromoCode = promoCode, InteracReference = reference });
        return null;
    }
    catch (ValidationException ex) { return ex.Message; }
}
ApplicationUser CreditPlayer(string name) => new(name, $"{name}@example.test", "test-only", "Credit", name, PaymentPlan.DropIn) { EmailConfirmed = true };

var creditNow = DateTime.UtcNow;
var halfCentPromo = new PromoCode("MATH-PCT", PromoDiscountType.Percent, 12.5m, creditNow.AddDays(-1), creditNow.AddDays(30), PromoAppliesTo.Both);
Assert(PaymentPricing.CalculateDiscount(halfCentPromo, 9m) == 1.13m && PaymentPricing.CalculateDiscount(halfCentPromo, 10m) == 1.25m,
    "percent discounts round half-cents away from zero");
Assert(PaymentPricing.CalculateDiscount(new PromoCode("MATH-FIX25", PromoDiscountType.Fixed, 25m, creditNow.AddDays(-1), creditNow.AddDays(30), PromoAppliesTo.Both), 10m) == 10m
    && PaymentPricing.CalculateDiscount(new PromoCode("MATH-FIX3", PromoDiscountType.Fixed, 3m, creditNow.AddDays(-1), creditNow.AddDays(30), PromoAppliesTo.Both), 10m) == 3m
    && PaymentPricing.CalculateDiscount(new PromoCode("MATH-ALL", PromoDiscountType.Percent, 100m, creditNow.AddDays(-1), creditNow.AddDays(30), PromoAppliesTo.Both), 10m) == 10m,
    "fixed discounts apply as-is and are capped at the price");
var seasonOnlyPromo = new PromoCode("MATH-SEASON", PromoDiscountType.Percent, 10m, creditNow.AddDays(-1), creditNow.AddDays(30), PromoAppliesTo.Season);
Assert(PaymentPricing.GetIneligibilityReason(seasonOnlyPromo, PaymentPlan.DropIn, creditNow) == "This promo code does not apply to this plan."
    && PaymentPricing.GetIneligibilityReason(seasonOnlyPromo, PaymentPlan.Season, creditNow) == null,
    "promo plan targeting maps PaymentPlan to PromoAppliesTo explicitly");

var creditSession = new Session(DateTime.UtcNow.Date.AddDays(6), 40, 10m, "10:00", "12:00", "Regression credit court");
var creditSession2 = new Session(DateTime.UtcNow.Date.AddDays(13), 40, 10m, "10:00", "12:00", "Regression credit court 2");
var limitPlayers = Enumerable.Range(0, 6).Select(i => CreditPlayer($"limit{i}")).ToArray();
var limitIds = limitPlayers.Select(p => p.Id).ToList();
var cappedPlayer = CreditPlayer("credit_cap");
var partialPlayer = CreditPlayer("credit_partial");
var spenderPlayer = CreditPlayer("credit_spender");
var interacPlayer = CreditPlayer("credit_interac");
var freePlayer = CreditPlayer("credit_free");
var referrerPlayer = CreditPlayer("ref_owner");
var refereePlayer = CreditPlayer("ref_friend");
var racerPlayer = CreditPlayer("ref_racer");
var ownerA = CreditPlayer("ref_owner_a");
var ownerB = CreditPlayer("ref_owner_b");
var limitPromo = new PromoCode("QA-LIMIT", PromoDiscountType.Fixed, 2m, creditNow.AddDays(-1), creditNow.AddDays(30), PromoAppliesTo.DropIn, maxUses: 2);
var pctPromo = new PromoCode("QA-PCT", PromoDiscountType.Percent, 12.5m, creditNow.AddDays(-1), creditNow.AddDays(30), PromoAppliesTo.Both);
var freePromo = new PromoCode("QA-FREE", PromoDiscountType.Percent, 100m, creditNow.AddDays(-1), creditNow.AddDays(30), PromoAppliesTo.Both);
await using (var db = Db()) {
    db.Sessions.AddRange(creditSession, creditSession2);
    db.Users.AddRange(limitPlayers);
    db.Users.AddRange(cappedPlayer, partialPlayer, spenderPlayer, interacPlayer, freePlayer, referrerPlayer, refereePlayer, racerPlayer, ownerA, ownerB);
    db.PromoCodes.AddRange(limitPromo, pctPromo, freePromo);
    db.AccountCredits.AddRange(
        new AccountCredit(cappedPlayer.Id, 25m, AccountCreditKind.ReferralReward, Guid.NewGuid()),
        new AccountCredit(partialPlayer.Id, 4m, AccountCreditKind.ReferralReward, Guid.NewGuid()),
        new AccountCredit(spenderPlayer.Id, 10m, AccountCreditKind.ReferralReward, Guid.NewGuid()));
    await db.SaveChangesAsync();
    var participation = new ParticipationRepository(db);
    foreach (var player in limitPlayers.Concat(new[] { cappedPlayer, partialPlayer, spenderPlayer, interacPlayer, freePlayer, refereePlayer }))
        await participation.ReserveAsync(creditSession.Id, player.Id);
    await participation.ReserveAsync(creditSession2.Id, spenderPlayer.Id);
    var flags = CreditFlags(db);
    await flags.SetEnabledAsync(FeatureFlagKeys.PromoCodes, true, null, "Regression");
    await flags.SetEnabledAsync(FeatureFlagKeys.Referrals, true, null, "Regression");
}

await using (var db = Db()) {
    var quote = await PaymentsFor(db).GetQuoteAsync(interacPlayer.Id, new PaymentQuoteRequestDto { Plan = PaymentPlan.DropIn, SessionId = creditSession.Id, PromoCode = " qa-pct " });
    Assert(quote is { OriginalAmount: 10m, DiscountAmount: 1.25m, CreditApplied: 0m, Total: 8.75m, PromoCode: "QA-PCT", PromoError: null, Locked: false },
        "quote applies a valid promo code case-insensitively");
    var creditQuote = await PaymentsFor(db).GetQuoteAsync(partialPlayer.Id, new PaymentQuoteRequestDto { Plan = PaymentPlan.DropIn, SessionId = creditSession.Id, PromoCode = "NOPE" });
    Assert(creditQuote is { DiscountAmount: 0m, CreditApplied: 4m, Total: 6m, PromoCode: null, PromoError: "This promo code was not found." },
        "quote explains an unusable promo code and still applies account credit");
    Assert(!await db.Payments.AnyAsync(p => p.SessionId == creditSession.Id) && (await db.PromoCodes.AsNoTracking().SingleAsync(p => p.Id == pctPromo.Id)).TimesUsed == 0
        && !await db.AccountCredits.AnyAsync(c => c.Kind == AccountCreditKind.AppliedToPayment), "quotes write nothing");
    await CreditFlags(db).SetEnabledAsync(FeatureFlagKeys.PromoCodes, false, null, "Regression");
    var flagOffQuote = await PaymentsFor(db).GetQuoteAsync(interacPlayer.Id, new PaymentQuoteRequestDto { Plan = PaymentPlan.DropIn, SessionId = creditSession.Id, PromoCode = "QA-PCT" });
    Assert(flagOffQuote is { DiscountAmount: 0m, Total: 10m, PromoError: "Promo codes are not available." }, "quote reports promo codes as unavailable while the flag is off");
    await CreditFlags(db).SetEnabledAsync(FeatureFlagKeys.PromoCodes, true, null, "Regression");
}

var limitResults = await Task.WhenAll(limitPlayers.Select(p => TryPayAsync(p.Id, creditSession.Id, "qa-limit")));
await using (var db = Db()) {
    var limitPayments = await db.Payments.AsNoTracking().Where(p => p.SessionId == creditSession.Id && limitIds.Contains(p.UserId)).ToListAsync();
    Assert(limitResults.Count(r => r == null) == 2 && limitResults.Where(r => r != null).All(r => r == PaymentPricing.PromoUsageLimitMessage),
        "six concurrent checkouts on a two-use promo code: exactly two get it, the rest hear it is used up");
    Assert((await db.PromoCodes.AsNoTracking().SingleAsync(p => p.Id == limitPromo.Id)).TimesUsed == 2
        && limitPayments.Count(p => p.PromoCodeId == limitPromo.Id && p is { DiscountAmount: 2m, Amount: 8m, OriginalAmount: 10m }) == 2
        && limitPayments.Count(p => p.PromoCodeId == null && p.Amount == 10m && p.DiscountAmount == 0m) == 4,
        "promo usage count matches the discounted payments and losers keep full price");
    var winnerId = limitPayments.First(p => p.PromoCodeId == limitPromo.Id).UserId;
    Assert(await TryPayAsync(winnerId, creditSession.Id, "QA-LIMIT") == null && await TryPayAsync(winnerId, creditSession.Id, "QA-PCT") == PaymentPricing.PromoAlreadyAppliedMessage
        && (await db.PromoCodes.AsNoTracking().SingleAsync(p => p.Id == limitPromo.Id)).TimesUsed == 2,
        "resending the applied promo changes nothing and a second promo is refused");
}

// The service pre-check can refuse losers before they reach the database; race the reservation itself.
var racePromo = new PromoCode("QA-RACE", PromoDiscountType.Fixed, 2m, creditNow.AddDays(-1), creditNow.AddDays(30), PromoAppliesTo.Both, maxUses: 2);
await using (var db = Db()) { db.PromoCodes.Add(racePromo); await db.SaveChangesAsync(); }
List<Guid> fullPriceIds;
await using (var db = Db())
    fullPriceIds = await db.Payments.Where(p => limitIds.Contains(p.UserId) && p.PromoCodeId == null).Select(p => p.Id).ToListAsync();
var reservationResults = await Task.WhenAll(fullPriceIds.Select(async id => {
    await using var db = Db();
    var payment = await db.Payments.SingleAsync(p => p.Id == id);
    return await new PaymentRepository(db).TryApplyAdjustmentsAsync(payment, new PaymentAdjustment(10m, 2m, 0m, racePromo.Id, racePromo.Id, 8m));
}));
await using (var db = Db()) {
    Assert(fullPriceIds.Count == 4 && reservationResults.Count(r => r == PaymentAdjustmentResult.Applied) == 2
        && reservationResults.Count(r => r == PaymentAdjustmentResult.PromoUnavailable) == 2
        && (await db.PromoCodes.AsNoTracking().SingleAsync(p => p.Id == racePromo.Id)).TimesUsed == 2
        && await db.Payments.CountAsync(p => p.PromoCodeId == racePromo.Id) == 2,
        "the atomic promo reservation lets exactly MaxUses concurrent adjustments through and rolls back the rest");
}

Assert(await TryPayAsync(cappedPlayer.Id, creditSession.Id, null) == null, "a player with more credit than the price can check out");
await using (var db = Db()) {
    var covered = await db.Payments.AsNoTracking().SingleAsync(p => p.UserId == cappedPlayer.Id);
    Assert(covered is { Status: PaymentStatus.Completed, CreditApplied: 10m, Amount: 0m, OriginalAmount: 10m } && covered.PaymentDate >= creditNow
        && await new AccountCreditRepository(db).GetBalanceAsync(cappedPlayer.Id) == 15m,
        "credit is capped at the price and a fully covered payment completes without Interac or Stripe");
}
Assert(await TryPayAsync(cappedPlayer.Id, creditSession.Id, null) == "This payment is not pending. Check your payment history.", "a completed payment cannot be paid again");
await using (var db = Db())
    Assert(await db.AccountCredits.CountAsync(c => c.UserId == cappedPlayer.Id && c.Kind == AccountCreditKind.AppliedToPayment) == 1, "a completed payment is never debited twice");

await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => TryPayAsync(partialPlayer.Id, creditSession.Id, null)));
await using (var db = Db()) { db.AccountCredits.Add(new AccountCredit(partialPlayer.Id, 5m, AccountCreditKind.ReferralReward, Guid.NewGuid())); await db.SaveChangesAsync(); }
Assert(await TryPayAsync(partialPlayer.Id, creditSession.Id, null) == null, "a partly covered payment can be retried");
Guid partialPaymentId;
await using (var db = Db()) {
    var partial = await db.Payments.AsNoTracking().SingleAsync(p => p.UserId == partialPlayer.Id);
    partialPaymentId = partial.Id;
    Assert(partial is { Status: PaymentStatus.Pending, CreditApplied: 4m, Amount: 6m } && await db.AccountCredits.CountAsync(c => c.PaymentId == partial.Id) == 1
        && await new AccountCreditRepository(db).GetBalanceAsync(partialPlayer.Id) == 5m,
        "credit is applied to a payment once, despite concurrent retries and credit that arrives later");
}

await Task.WhenAll(TryPayAsync(spenderPlayer.Id, creditSession.Id, null), TryPayAsync(spenderPlayer.Id, creditSession2.Id, null));
await using (var db = Db()) {
    var spent = await db.Payments.AsNoTracking().Where(p => p.UserId == spenderPlayer.Id).ToListAsync();
    Assert(spent.Count == 2 && spent.Sum(p => p.CreditApplied) == 10m && await new AccountCreditRepository(db).GetBalanceAsync(spenderPlayer.Id) == 0m,
        "two concurrent payments cannot spend the same credit twice");
}

Assert(await TryPayAsync(interacPlayer.Id, creditSession.Id, null, method: 0, reference: "BANK-REG-1") == null, "an Interac reference can be submitted");
Assert(await TryPayAsync(interacPlayer.Id, creditSession.Id, "QA-PCT") == PaymentPricing.PaymentLockedMessage, "a payment with a submitted Interac reference refuses a promo code");
await using (var db = Db()) {
    var locked = await db.Payments.AsNoTracking().SingleAsync(p => p.UserId == interacPlayer.Id);
    var lockedQuote = await PaymentsFor(db).GetQuoteAsync(interacPlayer.Id, new PaymentQuoteRequestDto { Plan = PaymentPlan.DropIn, SessionId = creditSession.Id, PromoCode = "QA-PCT" });
    Assert(locked is { DiscountAmount: 0m, Amount: 10m, PromoCodeId: null } && lockedQuote is { Locked: true, Total: 10m, PromoError: PaymentPricing.PaymentLockedMessage }
        && (await db.PromoCodes.AsNoTracking().SingleAsync(p => p.Id == pctPromo.Id)).TimesUsed == 0,
        "a locked payment keeps its price and the quote reports it as locked");
}

var cardMinPlayer = CreditPlayer("credit_cardmin");
var cardMinPromo = new PromoCode("QA-CARDMIN", PromoDiscountType.Fixed, 9.5m, creditNow.AddDays(-1), creditNow.AddDays(30), PromoAppliesTo.DropIn, maxUses: 5);
await using (var db = Db()) {
    db.Users.Add(cardMinPlayer);
    db.PromoCodes.Add(cardMinPromo);
    db.AccountCredits.Add(new AccountCredit(cardMinPlayer.Id, 0.2m, AccountCreditKind.ReferralReward, Guid.NewGuid()));
    await db.SaveChangesAsync();
    await new ParticipationRepository(db).ReserveAsync(creditSession.Id, cardMinPlayer.Id);
}
Assert(await TryPayAsync(cardMinPlayer.Id, creditSession.Id, "QA-CARDMIN") == PaymentPricing.BelowCardMinimumMessage,
    "card checkout refuses a total below Stripe's $0.50 minimum");
await using (var db = Db())
    Assert(!await db.Payments.AnyAsync(p => p.UserId == cardMinPlayer.Id) && (await db.PromoCodes.AsNoTracking().SingleAsync(p => p.Id == cardMinPromo.Id)).TimesUsed == 0
        && await new AccountCreditRepository(db).GetBalanceAsync(cardMinPlayer.Id) == 0.2m,
        "a refused sub-minimum card checkout reserves no promo use and debits no credit");
Assert(await TryPayAsync(cardMinPlayer.Id, creditSession.Id, "QA-CARDMIN", method: 0, reference: "BANK-CARDMIN") == null, "the same sub-minimum total can be paid by Interac");
await using (var db = Db())
    Assert(await db.Payments.AsNoTracking().SingleAsync(p => p.UserId == cardMinPlayer.Id) is { Amount: 0.3m, DiscountAmount: 9.5m, CreditApplied: 0.2m, Status: PaymentStatus.Pending },
        "an Interac payment keeps a sub-minimum total with the promo and credit applied");

string referrerCode;
await using (var db = Db()) {
    var referrals = ReferralsFor(db);
    var own = await referrals.GetOrCreateOwnCodeAsync(referrerPlayer.Id, "http://localhost");
    referrerCode = own.Code;
    Assert(own.RewardAmount == 10m, "referral code response carries the configured reward amount");
    await referrals.RedeemAsync(freePlayer.Id, referrerCode.ToLowerInvariant());
    await referrals.RedeemAsync(refereePlayer.Id, referrerCode);
}
Assert(await TryPayAsync(freePlayer.Id, creditSession.Id, "QA-FREE") == null, "a 100% promo code can be used at checkout");
await using (var db = Db()) {
    var free = await db.Payments.AsNoTracking().SingleAsync(p => p.UserId == freePlayer.Id);
    var freeRedemption = await db.ReferralRedemptions.AsNoTracking().SingleAsync(r => r.RefereeUserId == freePlayer.Id);
    Assert(free is { Status: PaymentStatus.Completed, DiscountAmount: 10m, Amount: 0m } && freeRedemption.RewardStatus == ReferralRewardStatus.Pending
        && !await db.AccountCredits.AnyAsync(c => c.ReferralRedemptionId == freeRedemption.Id),
        "a fully discounted payment completes but earns no referral reward");
}

Assert(await TryPayAsync(refereePlayer.Id, creditSession.Id, null) == null, "a referred player starts a card payment");
Guid refereePaymentId;
await using (var db = Db()) refereePaymentId = (await db.Payments.AsNoTracking().SingleAsync(p => p.UserId == refereePlayer.Id)).Id;
await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ => {
    await using var db = Db();
    await PaymentsFor(db).UpdatePaymentStatusAsync(refereePaymentId, PaymentStatus.Completed);
}));
await using (var db = Db()) {
    var redemption = await db.ReferralRedemptions.AsNoTracking().SingleAsync(r => r.RefereeUserId == refereePlayer.Id);
    var rewards = await db.AccountCredits.AsNoTracking().Where(c => c.UserId == referrerPlayer.Id && c.Kind == AccountCreditKind.ReferralReward).ToListAsync();
    Assert(rewards.Count == 1 && rewards[0].Amount == 10m && rewards[0].ReferralRedemptionId == redemption.Id && redemption.RewardStatus == ReferralRewardStatus.Granted,
        "concurrent completions of a referred player's first payment grant the reward exactly once");
    Assert(await db.Notifications.CountAsync(n => n.UserId == referrerPlayer.Id) == 1 && await db.AuditLogs.CountAsync(a => a.Action == "Referral.RewardGranted" && a.EntityId == redemption.Id) == 1,
        "the referrer is notified and the grant is audited once");
    await ReferralsFor(db).UpdateRedemptionStatusAsync(redemption.Id, (int)ReferralRewardStatus.Granted, null, "Regression");
    Assert(await db.AccountCredits.CountAsync(c => c.UserId == referrerPlayer.Id) == 1, "an admin grant of an already granted referral adds no credit");
}
await using (var db = Db()) {
    var referrals = ReferralsFor(db);
    var freeRedemptionId = (await db.ReferralRedemptions.AsNoTracking().SingleAsync(r => r.RefereeUserId == freePlayer.Id)).Id;
    await referrals.UpdateRedemptionStatusAsync(freeRedemptionId, (int)ReferralRewardStatus.Granted, null, "Regression");
    await referrals.UpdateRedemptionStatusAsync(freeRedemptionId, (int)ReferralRewardStatus.Granted, null, "Regression");
    var revokeRefused = false;
    try { await referrals.UpdateRedemptionStatusAsync(freeRedemptionId, (int)ReferralRewardStatus.Revoked, null, "Regression"); }
    catch (ValidationException) { revokeRefused = true; }
    Assert(revokeRefused && await db.AccountCredits.CountAsync(c => c.ReferralRedemptionId == freeRedemptionId) == 1
        && await new AccountCreditRepository(db).GetBalanceAsync(referrerPlayer.Id) == 20m,
        "an admin grant creates the credit once and revoking is only allowed from pending");
}

await using (var db = Db()) await PaymentsFor(db).UpdatePaymentStatusAsync(partialPaymentId, PaymentStatus.Failed);
await using (var db = Db()) await PaymentsFor(db).UpdatePaymentStatusAsync(partialPaymentId, PaymentStatus.Failed);
async Task<bool> StatusRefusedAsync(Guid paymentId, PaymentStatus status)
{
    try { await using var db = Db(); await PaymentsFor(db).UpdatePaymentStatusAsync(paymentId, status); return false; }
    catch (ValidationException) { return true; }
}
var refundAfterFailRefused = await StatusRefusedAsync(partialPaymentId, PaymentStatus.Refunded);
var completeAfterFailRefused = await StatusRefusedAsync(partialPaymentId, PaymentStatus.Completed);
await using (var db = Db()) {
    var releases = await db.AccountCredits.AsNoTracking().Where(c => c.PaymentId == partialPaymentId && c.Kind == AccountCreditKind.Released).ToListAsync();
    Assert(releases.Count == 1 && releases[0].Amount == 4m && await new AccountCreditRepository(db).GetBalanceAsync(partialPlayer.Id) == 9m,
        "credit from a failed payment is released exactly once");
    Assert(refundAfterFailRefused && completeAfterFailRefused && (await db.Payments.AsNoTracking().SingleAsync(p => p.Id == partialPaymentId)).Status == PaymentStatus.Failed,
        "a failed payment can't be marked completed or refunded, so released credit can't be spent twice");
}
await using (var db = Db()) await PaymentsFor(db).UpdatePaymentStatusAsync(partialPaymentId, PaymentStatus.Pending);
await using (var db = Db()) {
    var reopened = await db.Payments.AsNoTracking().SingleAsync(p => p.Id == partialPaymentId);
    Assert(reopened is { Status: PaymentStatus.Pending, Amount: 10m, CreditApplied: 0m } && await new AccountCreditRepository(db).GetBalanceAsync(partialPlayer.Id) == 9m,
        "reopening a failed payment restores the full charge instead of reusing released credit");
}
Assert(PaymentStatusRules.CanTransition(PaymentStatus.Pending, PaymentStatus.Completed) && PaymentStatusRules.CanTransition(PaymentStatus.Completed, PaymentStatus.Refunded)
    && !PaymentStatusRules.CanTransition(PaymentStatus.Refunded, PaymentStatus.Completed) && !PaymentStatusRules.CanTransition(PaymentStatus.Completed, PaymentStatus.Pending)
    && !PaymentStatusRules.CanTransition(PaymentStatus.Refunded, PaymentStatus.Pending),
    "payment status only moves forward: pending to completed or failed, completed to refunded");
Guid coveredPaymentId;
await using (var db = Db()) coveredPaymentId = (await db.Payments.AsNoTracking().SingleAsync(p => p.UserId == cappedPlayer.Id)).Id;
var completedEditRefused = false;
var negativeAmountRefused = false;
try { await using var db = Db(); await PaymentsFor(db).UpdatePaymentAsync(coveredPaymentId, new UpdatePaymentDto { Amount = 5m, Plan = PaymentPlan.DropIn, Status = PaymentStatus.Completed }); }
catch (ValidationException) { completedEditRefused = true; }
try { await using var db = Db(); await PaymentsFor(db).UpdatePaymentAsync(partialPaymentId, new UpdatePaymentDto { Amount = -1m, Plan = PaymentPlan.DropIn, Status = PaymentStatus.Pending }); }
catch (ValidationException) { negativeAmountRefused = true; }
await using (var db = Db())
    Assert(completedEditRefused && negativeAmountRefused && (await db.Payments.AsNoTracking().SingleAsync(p => p.Id == coveredPaymentId)).Amount == 0m,
        "a completed payment's amount is locked and amounts can't be negative");
Assert(TokenUserCheck.Evaluate(new AuthUserSnapshot(false, true, StaffRole.None), true, null) == null && TokenUserCheck.Evaluate(new AuthUserSnapshot(false, false, StaffRole.None), false, null) == null
    && TokenUserCheck.Evaluate(null, false, null) != null && TokenUserCheck.Evaluate(new AuthUserSnapshot(true, false, StaffRole.None), false, null) != null
    && TokenUserCheck.Evaluate(new AuthUserSnapshot(false, false, StaffRole.None), true, null) != null,
    "tokens stop working for missing, deactivated or demoted accounts");

RegisterUserDto Signup(string name, string? code) => new() {
    Username = name, Email = $"{name}@example.test", Password = "Regression!2026", FirstName = "Signup", LastName = name, PaymentPlan = PaymentPlan.DropIn, ReferralCode = code };
await using (var db = Db()) {
    db.ReferralCodes.Add(new ReferralCode("USEDUP22", cappedPlayer.Id, maxUses: 1) { TimesUsed = 1 });
    await db.SaveChangesAsync();
    var refusedMessages = new List<string>();
    foreach (var (name, code) in new[] { ("signup_badcode", "NOSUCH99"), ("signup_usedup", " usedup22 ") })
    {
        try { await UsersFor(db).RegisterAsync(Signup(name, code)); }
        catch (ValidationException ex) { refusedMessages.Add(ex.Message); }
    }
    Assert(refusedMessages.Count == 2 && refusedMessages.All(m => m == "Referral code not found or no longer valid.")
        && !await db.Users.AnyAsync(u => u.Username == "signup_badcode" || u.Username == "signup_usedup"),
        "sign-up with an unknown or used-up referral code is refused and creates no account");
    await UsersFor(db).RegisterAsync(Signup("signup_referred", " " + referrerCode.ToLowerInvariant() + " "));
    var signedUp = await db.Users.AsNoTracking().SingleAsync(u => u.Username == "signup_referred");
    Assert(await db.ReferralRedemptions.AnyAsync(r => r.RefereeUserId == signedUp.Id && r.ReferrerUserId == referrerPlayer.Id && r.RewardStatus == ReferralRewardStatus.Pending)
        && (await db.ReferralCodes.AsNoTracking().SingleAsync(c => c.Code == referrerCode)).TimesUsed == 3,
        "sign-up with a valid referral code records a pending redemption and counts the use");
}

await using (var db = Db()) {
    var referrals = ReferralsFor(db);
    var refereeCode = (await referrals.GetOrCreateOwnCodeAsync(refereePlayer.Id, "http://localhost")).Code;
    string? mutualMessage = null;
    try { await referrals.RedeemAsync(referrerPlayer.Id, refereeCode); }
    catch (ValidationException ex) { mutualMessage = ex.Message; }
    Assert(mutualMessage == "You cannot redeem the code of a player you referred" && !await db.ReferralRedemptions.AnyAsync(r => r.RefereeUserId == referrerPlayer.Id),
        "a player cannot redeem the code of someone they referred");
    string? paidMessage = null;
    try { await referrals.RedeemAsync(cappedPlayer.Id, refereeCode); }
    catch (ValidationException ex) { paidMessage = ex.Message; }
    await db.Users.Where(u => u.Id == ownerB.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.CreatedOn, DateTime.UtcNow.AddDays(-31)));
    string? lateMessage = null;
    try { await referrals.RedeemAsync(ownerB.Id, refereeCode); }
    catch (ValidationException ex) { lateMessage = ex.Message; }
    Assert(paidMessage?.Contains("not made a payment") == true && lateMessage?.Contains("30 days") == true,
        "players who already paid or joined over 30 days ago cannot redeem a referral code");
}

string ownerACode, ownerBCode;
await using (var db = Db()) {
    ownerACode = (await ReferralsFor(db).GetOrCreateOwnCodeAsync(ownerA.Id, "http://localhost")).Code;
    ownerBCode = (await ReferralsFor(db).GetOrCreateOwnCodeAsync(ownerB.Id, "http://localhost")).Code;
}
var raceResults = await Task.WhenAll(new[] { ownerACode, ownerBCode, ownerACode, ownerBCode }.Select(async code => {
    await using var db = Db();
    try { await ReferralsFor(db).RedeemAsync(racerPlayer.Id, code); return true; }
    catch (ValidationException) { return false; }
}));
await using (var db = Db()) {
    Assert(raceResults.Count(r => r) == 1 && await db.ReferralRedemptions.CountAsync(r => r.RefereeUserId == racerPlayer.Id) == 1
        && await db.ReferralCodes.Where(c => c.Code == ownerACode || c.Code == ownerBCode).SumAsync(c => c.TimesUsed) == 1,
        "concurrent redemptions by one player record one redemption, count one use and refuse the rest as validation errors");
}

await using (var db = Db()) {
    var promoAdmin = new PromoCodeService(new PromoCodeRepository(db), NullLogger<PromoCodeService>.Instance);
    UpsertPromoCodeDto Draft(string code) => new() { Code = code, DiscountType = PromoDiscountType.Percent, DiscountValue = 10m,
        ValidFrom = creditNow.AddDays(-1), ValidUntil = creditNow.AddDays(10), AppliesTo = PromoAppliesTo.Both };
    var drafts = new List<UpsertPromoCodeDto> { Draft("BADTYPE"), Draft("BADPLAN"), Draft("ZERO"), Draft("OVER100"), Draft(new string('X', 33)), Draft("NOUSES"), Draft("BACKWARDS") };
    drafts[0].DiscountType = (PromoDiscountType)7;
    drafts[1].AppliesTo = (PromoAppliesTo)9;
    drafts[2].DiscountValue = 0m;
    drafts[3].DiscountValue = 101m;
    drafts[5].MaxUses = 0;
    drafts[6].ValidUntil = drafts[6].ValidFrom.AddDays(-1);
    var rejected = 0;
    foreach (var draft in drafts)
    {
        try { await promoAdmin.CreateAsync(draft); }
        catch (ValidationException) { rejected++; }
    }
    Assert(rejected == drafts.Count && await db.PromoCodes.CountAsync() == 5,
        "promo admin rejects undefined enums, non-positive or over-100% values, over-long codes, MaxUses below 1 and backwards dates");
    var duplicateRefused = false;
    try { await promoAdmin.CreateAsync(Draft("qa-pct")); }
    catch (ValidationException) { duplicateRefused = true; }
    Assert(duplicateRefused && !await new PromoCodeRepository(db).TryAddAsync(new PromoCode("QA-PCT", PromoDiscountType.Fixed, 1m, creditNow, creditNow.AddDays(1), PromoAppliesTo.Both)),
        "a duplicate promo code is a validation error, including when the insert races");
    var negativeRefused = false;
    try { await promoAdmin.ValidateAsync(new ValidatePromoCodeDto { Code = "QA-PCT", TargetPlan = PromoAppliesTo.DropIn, Amount = -1m }); }
    catch (ValidationException) { negativeRefused = true; }
    var usedDeleteRefused = false;
    try { await promoAdmin.DeleteAsync(limitPromo.Id); }
    catch (ValidationException) { usedDeleteRefused = true; }
    Assert(negativeRefused && usedDeleteRefused, "promo validation rejects a negative amount and a used promo code cannot be deleted");
}

async Task<string?> TrySeasonAsync(Guid userId, Guid seasonId, int method, string? promoCode = null, string? reference = null)
{
    await using var db = Db();
    try
    {
        await PaymentsFor(db).CreateSeasonPaymentAsync(userId, new CreateSeasonPaymentDto { SeasonId = seasonId, PaymentMethod = method, PromoCode = promoCode, InteracReference = reference });
        return null;
    }
    catch (ValidationException ex) { return ex.Message; }
}
ApplicationUser SeasonPlayer(string name) => new(name, $"{name}@example.test", "test-only", "Season", name, PaymentPlan.Season) { EmailConfirmed = true };

var cardSeason = new Season(DateTime.UtcNow.Date.AddDays(-7), DateTime.UtcNow.Date.AddDays(60), 100m) { Name = "Card season" };
var seasonCardPlayer = SeasonPlayer("season_card");
var seasonCoveredPlayer = SeasonPlayer("season_covered");
var seasonMinPlayer = SeasonPlayer("season_min");
var seasonInteracPlayer = SeasonPlayer("season_interac");
var seasonMinPromo = new PromoCode("QA-SEASONMIN", PromoDiscountType.Fixed, 99.7m, creditNow.AddDays(-1), creditNow.AddDays(30), PromoAppliesTo.Season, maxUses: 5);
await using (var db = Db()) {
    db.Seasons.Add(cardSeason);
    db.Users.AddRange(seasonCardPlayer, seasonCoveredPlayer, seasonMinPlayer, seasonInteracPlayer);
    db.PromoCodes.Add(seasonMinPromo);
    db.AccountCredits.Add(new AccountCredit(seasonCoveredPlayer.Id, 100m, AccountCreditKind.ReferralReward, Guid.NewGuid()));
    await db.SaveChangesAsync();
}
Assert(await TrySeasonAsync(seasonCardPlayer.Id, cardSeason.Id, method: 1) == "Interac is currently the available season payment method.",
    "season card payment is refused while season-card-payments is off");
await using (var db = Db()) {
    Assert(!await db.Payments.AnyAsync(p => p.SeasonId == cardSeason.Id), "a refused season card payment creates nothing");
    await CreditFlags(db).SetEnabledAsync(FeatureFlagKeys.SeasonCardPayments, true, null, "Regression");
}
Assert(await TrySeasonAsync(seasonCardPlayer.Id, cardSeason.Id, method: 1) == null, "season card payment starts once the flag is on");
Assert(await TrySeasonAsync(seasonCoveredPlayer.Id, cardSeason.Id, method: 1) == null, "a season fee covered by credit completes through card checkout");
Assert(await TrySeasonAsync(seasonMinPlayer.Id, cardSeason.Id, method: 1, promoCode: "QA-SEASONMIN") == PaymentPricing.BelowCardMinimumMessage,
    "season card checkout refuses a total below Stripe's minimum");
Assert(await TrySeasonAsync(seasonInteracPlayer.Id, cardSeason.Id, method: 0, reference: "BANK-SEASON-1") == null
    && await TrySeasonAsync(seasonInteracPlayer.Id, cardSeason.Id, method: 1) == "Your Interac transfer is awaiting verification. Do not pay twice.",
    "a submitted season Interac transfer blocks a second payment by card");
await using (var db = Db()) {
    var seasonPayments = await db.Payments.AsNoTracking().Where(p => p.SeasonId == cardSeason.Id).ToListAsync();
    Assert(seasonPayments.Single(p => p.UserId == seasonCardPlayer.Id) is { Status: PaymentStatus.Pending, Amount: 100m, Plan: PaymentPlan.Season }
        && seasonPayments.Single(p => p.UserId == seasonCoveredPlayer.Id) is { Status: PaymentStatus.Completed, Amount: 0m, CreditApplied: 100m }
        && !seasonPayments.Any(p => p.UserId == seasonMinPlayer.Id)
        && (await db.PromoCodes.AsNoTracking().SingleAsync(p => p.Id == seasonMinPromo.Id)).TimesUsed == 0,
        "season card payments store the right amount and status, and a refused one reserves nothing");
}

var matchUser = Guid.NewGuid();
var matchSession = Guid.NewGuid();
var matchSeason = Guid.NewGuid();
Dictionary<string, string> CheckoutMeta(params (string Key, Guid Value)[] entries) => entries.ToDictionary(e => e.Key, e => e.Value.ToString());
Assert(StripeCheckoutMatch.Matches(CheckoutMeta(("userId", matchUser), ("sessionId", matchSession)), "cad", 1000, matchUser, matchSession, null, 10m)
    && StripeCheckoutMatch.Matches(CheckoutMeta(("userId", matchUser), ("seasonId", matchSeason)), "cad", 11050, matchUser, null, matchSeason, 110.5m),
    "Stripe checkout matching accepts a drop-in session and a season payment");
Assert(!StripeCheckoutMatch.Matches(CheckoutMeta(("userId", matchUser), ("seasonId", matchSeason)), "cad", 11049, matchUser, null, matchSeason, 110.5m)
    && !StripeCheckoutMatch.Matches(CheckoutMeta(("userId", matchUser), ("seasonId", Guid.NewGuid())), "cad", 11050, matchUser, null, matchSeason, 110.5m)
    && !StripeCheckoutMatch.Matches(CheckoutMeta(("userId", matchUser), ("sessionId", matchSession), ("seasonId", matchSeason)), "cad", 11050, matchUser, null, matchSeason, 110.5m)
    && !StripeCheckoutMatch.Matches(CheckoutMeta(("userId", Guid.NewGuid()), ("seasonId", matchSeason)), "cad", 11050, matchUser, null, matchSeason, 110.5m)
    && !StripeCheckoutMatch.Matches(CheckoutMeta(("userId", matchUser), ("seasonId", matchSeason)), "usd", 11050, matchUser, null, matchSeason, 110.5m)
    && !StripeCheckoutMatch.Matches(CheckoutMeta(("userId", matchUser), ("sessionId", matchSession)), "cad", 1000, matchUser, null, matchSession, 10m),
    "Stripe checkout matching rejects a wrong amount, season, owner or currency, and a session id presented as a season");

PaymentRefundService RefundsFor(ApplicationDbContext db) => new(new PaymentRepository(db), new AccountCreditRepository(db), null!, PaymentsFor(db), NullLogger<PaymentRefundService>.Instance);
async Task<string?> TryRefundAsync(Guid paymentId, RefundMethod method, string? reason)
{
    try { await using var db = Db(); await RefundsFor(db).RefundAsync(paymentId, new RefundPaymentDto { Method = method, Reason = reason }); return null; }
    catch (ValidationException ex) { return ex.Message; }
}
Guid cardMinPaymentId;
await using (var db = Db()) cardMinPaymentId = (await db.Payments.AsNoTracking().SingleAsync(p => p.UserId == cardMinPlayer.Id)).Id;
var pendingRefund = await TryRefundAsync(cardMinPaymentId, RefundMethod.AccountCredit, "Too early");
await using (var db = Db()) await PaymentsFor(db).UpdatePaymentStatusAsync(cardMinPaymentId, PaymentStatus.Completed);
var reasonMissing = await TryRefundAsync(cardMinPaymentId, RefundMethod.AccountCredit, "   ");
var notCard = await TryRefundAsync(cardMinPaymentId, RefundMethod.Card, "Card refund");
var firstRefund = await TryRefundAsync(cardMinPaymentId, RefundMethod.AccountCredit, "Session moved");
var repeatRefund = await TryRefundAsync(cardMinPaymentId, RefundMethod.AccountCredit, "Session moved");
Assert(pendingRefund == PaymentRefundService.NotCompletedMessage && reasonMissing == PaymentRefundService.ReasonRequiredMessage
    && notCard == PaymentRefundService.NotCardPaymentMessage && firstRefund == null && repeatRefund == null,
    "a refund needs a completed payment, a reason and a card payment for card refunds; repeating it is harmless");
await using (var db = Db()) {
    var refundedPayment = await db.Payments.AsNoTracking().SingleAsync(p => p.Id == cardMinPaymentId);
    var ledger = await db.AccountCredits.AsNoTracking().Where(c => c.PaymentId == cardMinPaymentId).ToListAsync();
    Assert(refundedPayment is { Status: PaymentStatus.Refunded, RefundMethod: RefundMethod.AccountCredit, RefundReason: "Session moved" } && refundedPayment.RefundedOn != null
        && ledger.Count(c => c.Kind == AccountCreditKind.Refund) == 1 && ledger.Single(c => c.Kind == AccountCreditKind.Refund).Amount == 0.3m
        && ledger.Single(c => c.Kind == AccountCreditKind.Released).Amount == 0.2m
        && await new AccountCreditRepository(db).GetBalanceAsync(cardMinPlayer.Id) == 0.5m,
        "refunding as credit returns the amount paid once and releases the credit the payment used");
}
AccountCreditService CreditAdminFor(ApplicationDbContext db) => new(new AccountCreditRepository(db), new UserRepository(db, NullLogger<UserRepository>.Instance));
async Task<string?> TryAdjustAsync(Guid userId, decimal amount, string? note)
{
    try { await using var db = Db(); await CreditAdminFor(db).AdjustAsync(userId, amount, note, winner); return null; }
    catch (ValidationException ex) { return ex.Message; }
}
var zeroAdjust = await TryAdjustAsync(cardMinPlayer.Id, 0m, "Nothing");
var noteMissing = await TryAdjustAsync(cardMinPlayer.Id, 5m, " ");
var tooLarge = await TryAdjustAsync(cardMinPlayer.Id, 600m, "Too much");
var belowZero = await TryAdjustAsync(cardMinPlayer.Id, -5m, "Correction");
var removeAll = await TryAdjustAsync(cardMinPlayer.Id, -0.5m, "Correction");
var goodwill = await TryAdjustAsync(cardMinPlayer.Id, 2m, "Goodwill for the rain delay");
await using (var db = Db()) {
    var manual = await db.AccountCredits.AsNoTracking().Where(c => c.UserId == cardMinPlayer.Id && c.Kind == AccountCreditKind.ManualAdjustment).ToListAsync();
    Assert(zeroAdjust != null && noteMissing != null && tooLarge != null && belowZero != null && removeAll == null && goodwill == null
        && manual.Count == 2 && manual.All(c => c.CreatedByUserId == winner && !string.IsNullOrEmpty(c.Note))
        && await new AccountCreditRepository(db).GetBalanceAsync(cardMinPlayer.Id) == 2m,
        "admin credit adjustments need a note, stay within limits, never go below zero and record who made them");
}
var voidPlayer = CreditPlayer("credit_void");
await using (var db = Db()) {
    db.Users.Add(voidPlayer);
    db.AccountCredits.Add(new AccountCredit(voidPlayer.Id, 4m, AccountCreditKind.ReferralReward, Guid.NewGuid()));
    await db.SaveChangesAsync();
    await new ParticipationRepository(db).ReserveAsync(creditSession2.Id, voidPlayer.Id);
}
Assert(await TryPayAsync(voidPlayer.Id, creditSession2.Id, null) == null, "a player with partial credit starts a card payment for a session that will be cancelled");
Guid spenderPaymentId;
decimal spenderCredit;
await using (var db = Db()) {
    var spent = await db.Payments.AsNoTracking().SingleAsync(p => p.UserId == voidPlayer.Id);
    spenderPaymentId = spent.Id;
    spenderCredit = spent.CreditApplied;
}
bool voidedOnce, voidedTwice;
await using (var db = Db()) voidedOnce = await PaymentsFor(db).VoidForCancelledSessionAsync(spenderPaymentId);
await using (var db = Db()) voidedTwice = await PaymentsFor(db).VoidForCancelledSessionAsync(spenderPaymentId);
await using (var db = Db()) {
    var voided = await db.Payments.AsNoTracking().SingleAsync(p => p.Id == spenderPaymentId);
    var releases = await db.AccountCredits.AsNoTracking().Where(c => c.PaymentId == spenderPaymentId && c.Kind == AccountCreditKind.Released).ToListAsync();
    Assert(voidedOnce && !voidedTwice && voided.Status == PaymentStatus.Failed && releases.Count == 1 && releases[0].Amount == spenderCredit
        && spenderCredit == 4m && await new AccountCreditRepository(db).GetBalanceAsync(voidPlayer.Id) == 4m,
        "voiding a cancelled session's pending payment returns its credit exactly once");
}
var notReceivedPayment = new Payment(winner, 15m, PaymentPlan.DropIn) { Reference = "QA-NOTRECEIVED|INTERAC:BANK-404" };
var plainPendingPayment = new Payment(winner, 16m, PaymentPlan.DropIn) { Reference = "QA-PLAIN" };
await using (var db = Db()) { db.Payments.AddRange(notReceivedPayment, plainPendingPayment); await db.SaveChangesAsync(); }
SaintHenriBasketball.Application.DTOs.Reconciliation.BulkMarkNotReceivedResultDto firstMark, secondMark;
await using (var db = Db())
    firstMark = await new ReconciliationService(new PaymentRepository(db), PaymentsFor(db), new AuditLogRepository(db), NullLogger<ReconciliationService>.Instance)
        .BulkMarkNotReceivedAsync(new[] { notReceivedPayment.Id, plainPendingPayment.Id }, "Not in Tangerine after 10 days", winner, "Regression");
await using (var db = Db())
    secondMark = await new ReconciliationService(new PaymentRepository(db), PaymentsFor(db), new AuditLogRepository(db), NullLogger<ReconciliationService>.Instance)
        .BulkMarkNotReceivedAsync(new[] { notReceivedPayment.Id }, null, winner, "Regression");
await using (var db = Db())
    Assert(firstMark is { MarkedNotReceived: 1, Skipped: 1, Errors: 0 } && secondMark is { MarkedNotReceived: 0, Skipped: 1 }
        && (await db.Payments.AsNoTracking().SingleAsync(p => p.Id == notReceivedPayment.Id)).Status == PaymentStatus.Failed
        && (await db.Payments.AsNoTracking().SingleAsync(p => p.Id == plainPendingPayment.Id)).Status == PaymentStatus.Pending
        && await db.AuditLogs.CountAsync(a => a.EntityId == notReceivedPayment.Id && a.Action == "Payment.InteracNotReceived" && a.Details!.Contains("Tangerine")) == 1,
        "an Interac transfer marked not received fails once, is audited with the note, and other payments are skipped");
AccountLifecycleService LifecycleFor(ApplicationDbContext db, RecordingCache? cache = null) => new(new UserRepository(db, NullLogger<UserRepository>.Instance),
    new SessionRegistrationRepository(db), new ParticipationRepository(db), cache ?? new RecordingCache(), NullLogger<AccountLifecycleService>.Instance);
await using (var db = Db()) {
    var hardDeleteRefused = false;
    db.Users.Remove(await db.Users.SingleAsync(u => u.Id == partialPlayer.Id));
    try { await db.SaveChangesAsync(); }
    catch (DbUpdateException) { hardDeleteRefused = true; }
    Assert(hardDeleteRefused, "a player with payments can't be hard-deleted");
}
await using (var db = Db()) await LifecycleFor(db).DeactivateAsync(partialPlayer.Id, anonymize: true);
var anonymizedReactivateRefused = false;
try { await using var db = Db(); await LifecycleFor(db).ReactivateAsync(partialPlayer.Id); }
catch (ValidationException) { anonymizedReactivateRefused = true; }
await using (var db = Db()) {
    var closed = await db.Users.AsNoTracking().SingleAsync(u => u.Id == partialPlayer.Id);
    Assert(closed.IsDeactivated && closed.AnonymizedOn != null && closed.Email!.EndsWith("@deleted.invalid") && closed.FirstName == "Former" && closed.EmergencyContactName == null
        && await db.Payments.AnyAsync(p => p.UserId == partialPlayer.Id) && await db.AccountCredits.AnyAsync(c => c.UserId == partialPlayer.Id) && anonymizedReactivateRefused,
        "closing an account erases personal details but keeps payments and the credit ledger");
}
var lifecycleCache = new RecordingCache();
await using (var db = Db()) await LifecycleFor(db, lifecycleCache).DeactivateAsync(spenderPlayer.Id, anonymize: false);
var cacheKeysSample = SessionCacheKeys.For(session.Id, new[] { spenderPlayer.Id, spenderPlayer.Id });
Assert(lifecycleCache.Removed.Contains($"Attendance:User:{spenderPlayer.Id}") && lifecycleCache.Removed.Contains(SessionCacheKeys.UpcomingSessions)
    && cacheKeysSample.Contains($"Attendance:Session:{session.Id}:Attendees") && cacheKeysSample.Contains($"Attendance:Session:{session.Id}:Summary")
    && cacheKeysSample.Count(k => k == $"Attendance:User:{spenderPlayer.Id}") == 1,
    "deactivating clears the player's cached bookings, and session cache keys cover attendees and the summary");
await using (var db = Db()) {
    var deactivated = await db.Users.AsNoTracking().SingleAsync(u => u.Id == spenderPlayer.Id);
    Assert(deactivated.IsDeactivated && deactivated.AnonymizedOn == null && deactivated.Email == spenderPlayer.Email
        && !await db.SessionRegistrations.AnyAsync(r => r.UserId == spenderPlayer.Id && r.Session.SessionDate >= DateTime.UtcNow.Date),
        "deactivating keeps personal details and releases upcoming reservations");
}
await using (var db = Db()) await LifecycleFor(db).ReactivateAsync(spenderPlayer.Id);
await using (var db = Db())
    Assert(!(await db.Users.AsNoTracking().SingleAsync(u => u.Id == spenderPlayer.Id)).IsDeactivated, "a deactivated player can be reactivated");

// Admin lists: filtering, totals and paging run in the database.
Assert(ListPaging.Clamp(0, 0) == (1, 50) && ListPaging.Clamp(3, 10_000) == (3, ListPaging.MaxPageSize), "admin list paging stays in bounds");
Assert(EngagementTiers.AttendedRange(EngagementTiers.High, 10) == (8, null)
    && EngagementTiers.AttendedRange(EngagementTiers.Medium, 3) == (2, 3)
    && EngagementTiers.AttendedRange(EngagementTiers.Inactive, 0) == (0, null)
    && EngagementTiers.AttendedRange(EngagementTiers.Low, 0)!.Value.Min == int.MaxValue
    && EngagementTiers.AttendedRange("Nope", 5) == null
    && EngagementTiers.Tier(EngagementTiers.Rate(2, 3)) == EngagementTiers.Medium
    && EngagementTiers.Tier(EngagementTiers.Rate(3, 3)) == EngagementTiers.High, "engagement tiers match their attendance ranges");
var directoryAlpha = new ApplicationUser("diralpha", "dir-alpha@example.test", "test-only", "Directory", "Alpha", PaymentPlan.Season) { EmailConfirmed = true };
var directoryAdmin = new ApplicationUser("diradmin", "dir-admin@example.test", "test-only", "Directory", "Admin", PaymentPlan.DropIn) { EmailConfirmed = true, IsAdmin = true };
var directoryGone = new ApplicationUser("dirgone", "dir-gone@example.test", "test-only", "Directory", "Gone", PaymentPlan.DropIn) { EmailConfirmed = true, IsDeactivated = true };
var searchStamp = DateTime.UtcNow.Date.AddYears(-3); // no other check writes payments this far back
await using (var db = Db())
{
    db.Users.AddRange(directoryAlpha, directoryAdmin, directoryGone);
    db.Payments.AddRange(
        new Payment(directoryAlpha.Id, 110m, PaymentPlan.Season) { Status = PaymentStatus.Completed, PaymentDate = searchStamp, Reference = "DIRSEARCH-SEASON" },
        new Payment(directoryAlpha.Id, 10m, PaymentPlan.DropIn) { Status = PaymentStatus.Completed, PaymentDate = searchStamp.AddDays(1), Reference = "DIRSEARCH-DROP1" },
        new Payment(directoryAlpha.Id, 10m, PaymentPlan.DropIn) { Status = PaymentStatus.Pending, PaymentDate = searchStamp.AddDays(2), Reference = "DIRSEARCH-DROP2" },
        new Payment(directoryAdmin.Id, 20m, PaymentPlan.DropIn) { Status = PaymentStatus.Refunded, PaymentDate = searchStamp.AddDays(3), Reference = "DIRSEARCH-REFUND" });
    db.SessionAttendances.Add(new SessionAttendance { Id = Guid.NewGuid(), SessionId = session.Id, UserId = directoryAlpha.Id, IsAttending = true, CreatedOn = DateTime.UtcNow, LastUpdated = DateTime.UtcNow });
    await db.SaveChangesAsync();
}
await using (var db = Db())
{
    var search = PaymentsFor(db);
    var window = new PaymentSearchCriteria(From: searchStamp.AddMinutes(-1), To: searchStamp.AddDays(3).AddMinutes(1), PageSize: 2);
    var firstPage = await search.SearchPaymentsAsync(window);
    var secondPage = await search.SearchPaymentsAsync(window with { Page = 2 });
    Assert(firstPage.Total == 4 && firstPage.Items.Select(p => p.Reference).SequenceEqual(new[] { "DIRSEARCH-REFUND", "DIRSEARCH-DROP2" })
        && secondPage.Items.Select(p => p.Reference).SequenceEqual(new[] { "DIRSEARCH-DROP1", "DIRSEARCH-SEASON" })
        && firstPage.Summary.CompletedCount == 2 && firstPage.Summary.Collected == 120m
        && firstPage.Summary.SeasonCollected == 110m && firstPage.Summary.DropInCollected == 10m,
        "payment search pages newest first and totals only collected money across every page");
    var byName = await search.SearchPaymentsAsync(new PaymentSearchCriteria(Search: " directory alpha ", Status: PaymentStatus.Completed, Plan: PaymentPlan.DropIn));
    var byReference = await search.SearchPaymentsAsync(new PaymentSearchCriteria(Search: "dirsearch-refund"));
    Assert(byName.Total == 1 && byName.Items[0].Reference == "DIRSEARCH-DROP1" && byReference.Total == 1 && byReference.Items[0].UserEmail == directoryAdmin.Email,
        "payment search matches a full name or reference together with status and plan filters");
    var reversedRangeRefused = false;
    try { await search.SearchPaymentsAsync(new PaymentSearchCriteria(From: searchStamp, To: searchStamp.AddDays(-1))); }
    catch (ValidationException) { reversedRangeRefused = true; }
    Assert(reversedRangeRefused, "payment search refuses a start date after the end date");
}
await using (var db = Db())
{
    var directory = new UserDirectoryService(new UserRepository(db, NullLogger<UserRepository>.Instance), new SessionRepository(db), creditMapper);
    var active = await directory.SearchAsync(new UserDirectoryQuery { Search = "directory" });
    var deactivated = await directory.SearchAsync(new UserDirectoryQuery { Search = "directory", Account = UserAccountFilter.Deactivated });
    var everyone = await directory.SearchAsync(new UserDirectoryQuery { Search = "dir-", Account = UserAccountFilter.All, PageSize = 2 });
    var seasonPlayers = await directory.SearchAsync(new UserDirectoryQuery { Search = "directory", IsAdmin = false, Plan = PaymentPlan.Season });
    Assert(active.Total == 2 && active.AdminCount == 1 && active.Items.Select(i => i.User.Username).SequenceEqual(new[] { "diradmin", "diralpha" })
        && deactivated.Total == 1 && deactivated.Items[0].User.IsDeactivated
        && everyone.Total == 3 && everyone.Items.Count == 2
        && seasonPlayers.Total == 1 && seasonPlayers.Items[0].User.Username == "diralpha",
        "player directory searches, filters by account, role and plan, and pages on the server");
    var byAttendance = await directory.SearchAsync(new UserDirectoryQuery { Search = "directory", Sort = UserSearchSort.Attendance });
    var alphaItem = byAttendance.Items[0];
    var sameTier = await directory.SearchAsync(new UserDirectoryQuery { Search = "directory", Engagement = alphaItem.EngagementTier.ToLowerInvariant() });
    var inactive = await directory.SearchAsync(new UserDirectoryQuery { Search = "directory", Engagement = EngagementTiers.Inactive });
    var unknownTierRefused = false;
    try { await directory.SearchAsync(new UserDirectoryQuery { Engagement = "Superstar" }); }
    catch (ValidationException) { unknownTierRefused = true; }
    Assert(alphaItem.User.Username == "diralpha" && alphaItem.AttendanceRate > 0 && EngagementTiers.Tier(alphaItem.AttendanceRate) == alphaItem.EngagementTier
        && sameTier.Items.Any(i => i.User.Username == "diralpha") && inactive.Items.Any(i => i.User.Username == "diradmin")
        && inactive.Items.All(i => i.EngagementTier == EngagementTiers.Inactive) && unknownTierRefused,
        "player directory sorts and filters by engagement computed from recent attendance");
}

// Deleting a session: payments block it; otherwise its registrations and attendance go with it.
var deletableSession = new Session(DateTime.UtcNow.Date.AddDays(20), 1, 10, "10:00", "12:00", "Delete me court");
var paidSession = new Session(DateTime.UtcNow.Date.AddDays(21), 1, 10, "10:00", "12:00", "Paid court");
await using (var db = Db())
{
    db.Sessions.AddRange(deletableSession, paidSession);
    db.SessionRegistrations.Add(new SessionRegistration(directoryAlpha.Id, deletableSession.Id, PaymentPlan.Season));
    db.SessionAttendances.Add(new SessionAttendance { Id = Guid.NewGuid(), SessionId = deletableSession.Id, UserId = directoryAlpha.Id, IsAttending = true, CreatedOn = DateTime.UtcNow, LastUpdated = DateTime.UtcNow });
    db.Payments.Add(new Payment(directoryAlpha.Id, 10m, PaymentPlan.DropIn, paidSession.Id) { Reference = "DELETE-BLOCKED", CreatedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();
}
await using (var db = Db())
{
    var deletionCache = new RecordingCache();
    var deletion = new SessionDeletionService(new SessionRepository(db), deletionCache);
    var deletablePreview = await deletion.PreviewAsync(deletableSession.Id);
    var paidPreview = await deletion.PreviewAsync(paidSession.Id);
    var paidRefused = false;
    try { await deletion.DeleteAsync(paidSession.Id); }
    catch (ValidationException) { paidRefused = true; }
    var removed = await deletion.DeleteAsync(deletableSession.Id);
    var goneAfterwards = false;
    try { await deletion.PreviewAsync(deletableSession.Id); }
    catch (NotFoundException) { goneAfterwards = true; }
    Assert(deletablePreview.CanDelete && deletablePreview.Registrations == 1 && deletablePreview.AttendanceAnswers == 1
        && !paidPreview.CanDelete && paidPreview.Payments == 1 && paidRefused && removed.Registrations == 1 && goneAfterwards
        && !await db.SessionRegistrations.AnyAsync(r => r.SessionId == deletableSession.Id)
        && !await db.SessionAttendances.AnyAsync(a => a.SessionId == deletableSession.Id)
        && await db.Sessions.AnyAsync(s => s.Id == paidSession.Id)
        && deletionCache.Removed.Contains($"Attendance:Session:{deletableSession.Id}:Attendees"),
        "deleting a session removes its registrations and attendance, and a session with payments can't be deleted");
}

// Audit: automatic entries for admin changes, and a searchable log with totals.
var recapRouteId = Guid.NewGuid();
var describedRecap = AdminAuditPolicy.Describe("SessionRecaps", "Delete",
    new[] { new KeyValuePair<string, string?>("version", "1"), new("sessionId", session.Id.ToString()), new("recapId", recapRouteId.ToString()) },
    "delete", "/api/v1/admin/sessions/x/recaps/y");
Assert(AdminAuditPolicy.RequiresAdmin(new string?[] { null, "Admin" }) && AdminAuditPolicy.RequiresAdmin(new string?[] { "Player, Admin" })
    && !AdminAuditPolicy.RequiresAdmin(new string?[] { null, "Player" })
    && AdminAuditPolicy.ShouldAudit("delete", true, 204) && !AdminAuditPolicy.ShouldAudit("GET", true, 200)
    && !AdminAuditPolicy.ShouldAudit("POST", true, 400) && !AdminAuditPolicy.ShouldAudit("POST", false, 200)
    && describedRecap.EntityId == recapRouteId && describedRecap.EntityType == "SessionRecaps" && describedRecap.Details == "DELETE /api/v1/admin/sessions/x/recaps/y",
    "only successful admin-only changes are audited automatically, recorded against the last id in the route");
var auditActor = Guid.NewGuid();
await using (var db = Db())
{
    var audit = new AuditLogService(new AuditLogRepository(db));
    await audit.LogAsync("Created", "RegressionAudit", null, "first", auditActor, "Audit Admin");
    await audit.LogAsync("Deleted", "RegressionAudit", null, "second", auditActor, "Audit Admin");
    await audit.LogAsync("Created", "RegressionAuditOther", null, "third", null, "System");
    var byType = await audit.SearchAsync(new AuditLogQuery { EntityType = "RegressionAudit", PageSize = 1 });
    var byActorAndAction = await audit.SearchAsync(new AuditLogQuery { UserId = auditActor, Action = "delet" });
    var laterOnly = await audit.SearchAsync(new AuditLogQuery { EntityType = "RegressionAudit", From = DateTime.UtcNow.AddMinutes(5) });
    var auditFilters = await audit.GetFiltersAsync();
    var reversedAuditRange = false;
    try { await audit.SearchAsync(new AuditLogQuery { From = DateTime.UtcNow, To = DateTime.UtcNow.AddDays(-1) }); }
    catch (ValidationException) { reversedAuditRange = true; }
    Assert(byType.Total == 2 && byType.Items.Count == 1 && byActorAndAction.Total == 1 && byActorAndAction.Items[0].Details == "second"
        && laterOnly.Total == 0 && byType.Items[0].CreatedAt.Kind == DateTimeKind.Utc && auditFilters.EntityTypes.Contains("RegressionAudit") && auditFilters.EntityTypes.Contains("RegressionAuditOther")
        && auditFilters.Actors.Any(a => a.UserId == auditActor && a.UserName == "Audit Admin") && reversedAuditRange,
        "the audit log filters by admin, entity type, action and date, reports the total, and lists its filter choices");
}

// Broadcast history: each send is recorded with its sender, message and delivery counts.
await using (var db = Db())
{
    var broadcastHistory = new BroadcastRepository(db);
    var historyService = new BroadcastService(new UserRepository(db, NullLogger<UserRepository>.Instance), null!, null!, null!, null!, null!, null!,
        new BroadcastQueue(), NullLogger<BroadcastService>.Instance, broadcastHistory);
    var queuedBroadcast = await historyService.QueueAsync(
        new SendBroadcastRequestDto { Audience = BroadcastAudience.All, Subject = "  History check  ", BodyEn = "Hello", BodyFr = " " },
        directoryAdmin.Id, "Directory Admin");
    var queuedEntry = (await historyService.GetHistoryAsync(1, 10)).Items[0];
    await broadcastHistory.RecordDeliveryAsync(queuedBroadcast.BroadcastId!.Value, BroadcastStatus.Sent, queuedBroadcast.Attempted, queuedBroadcast.Attempted, 0);
    await broadcastHistory.MarkFailedAsync(queuedBroadcast.BroadcastId.Value);
    var longSubjectRefused = false;
    try { await historyService.QueueAsync(new SendBroadcastRequestDto { Subject = new string('x', 201), BodyEn = "Body" }, null, "Admin"); }
    catch (ValidationException) { longSubjectRefused = true; }
    var broadcastPage = await historyService.GetHistoryAsync(0, 1000);
    var broadcastDetail = await historyService.GetBroadcastAsync(queuedBroadcast.BroadcastId.Value);
    var unknownBroadcast = false;
    try { await historyService.GetBroadcastAsync(Guid.NewGuid()); }
    catch (NotFoundException) { unknownBroadcast = true; }
    Assert(queuedBroadcast.Queued && queuedEntry.Status == "Queued" && broadcastPage.Total == 1 && broadcastPage.PageSize == BroadcastService.MaxHistoryPageSize
        && broadcastPage.Items[0].Subject == "History check" && broadcastPage.Items[0].Status == "Sent" && broadcastPage.Items[0].SentByName == "Directory Admin"
        && broadcastPage.Items[0].CompletedAt?.Kind == DateTimeKind.Utc && broadcastPage.Items[0].QueuedAt.Kind == DateTimeKind.Utc
        && broadcastDetail.BodyEn == "Hello" && broadcastDetail.BodyFr == null
        && longSubjectRefused && unknownBroadcast,
        "each broadcast is recorded with its sender, message and delivery counts, and a sent one isn't later marked failed");
}

// Broadcast audiences: recent no-shows come from two club-wide queries, and deactivated players are never included.
BroadcastService AudienceFor(ApplicationDbContext db) => new(new UserRepository(db, NullLogger<UserRepository>.Instance), new SessionRegistrationRepository(db),
    new SessionAttendanceRepository(db, NullLogger<SessionAttendanceRepository>.Instance), null!, null!, null!, null!, new BroadcastQueue(), NullLogger<BroadcastService>.Instance, new BroadcastRepository(db));
int noShowsBefore, everyoneBefore;
await using (var db = Db())
{
    noShowsBefore = (await AudienceFor(db).PreviewAudienceAsync(BroadcastAudience.RecentNoShows)).RecipientCount;
    everyoneBefore = (await AudienceFor(db).PreviewAudienceAsync(BroadcastAudience.All)).RecipientCount;
}
var noShowSession = new Session(DateTime.UtcNow.Date.AddDays(-5), 1, 10, "10:00", "12:00", "No-show court");
var cameToSession = new ApplicationUser("noshowcame", "noshow-came@example.test", "test-only", "Came", "Player", PaymentPlan.DropIn) { EmailConfirmed = true };
var missedSession = new ApplicationUser("noshowmissed", "noshow-missed@example.test", "test-only", "Missed", "Player", PaymentPlan.DropIn) { EmailConfirmed = true };
var deactivatedNoShow = new ApplicationUser("noshowgone", "noshow-gone@example.test", "test-only", "Gone", "Player", PaymentPlan.DropIn) { EmailConfirmed = true, IsDeactivated = true };
await using (var db = Db())
{
    db.Sessions.Add(noShowSession);
    db.Users.AddRange(cameToSession, missedSession, deactivatedNoShow);
    db.SessionRegistrations.AddRange(
        new SessionRegistration(cameToSession.Id, noShowSession.Id, PaymentPlan.DropIn),
        new SessionRegistration(missedSession.Id, noShowSession.Id, PaymentPlan.DropIn),
        new SessionRegistration(deactivatedNoShow.Id, noShowSession.Id, PaymentPlan.DropIn));
    db.SessionAttendances.Add(new SessionAttendance { Id = Guid.NewGuid(), SessionId = noShowSession.Id, UserId = cameToSession.Id, IsAttending = true, CreatedOn = DateTime.UtcNow, LastUpdated = DateTime.UtcNow });
    await db.SaveChangesAsync();
}
await using (var db = Db())
{
    var noShowsAfter = await AudienceFor(db).PreviewAudienceAsync(BroadcastAudience.RecentNoShows);
    var everyoneAfter = await AudienceFor(db).PreviewAudienceAsync(BroadcastAudience.All);
    Assert(noShowsAfter.RecipientCount - noShowsBefore == 1 && everyoneAfter.RecipientCount - everyoneBefore == 2
        && noShowsAfter.SampleEmails.Contains(missedSession.Email!) && !noShowsAfter.SampleEmails.Contains(cameToSession.Email!),
        "recent no-shows are players who skipped their recent sessions, and deactivated players never receive broadcasts");
}
// Waitlist rejoin: one row per player and session, so leaving and joining again reuses it instead of failing.
var rejoinSession = new Session(DateTime.UtcNow.Date.AddDays(30), 1, 10, "10:00", "12:00", "Rejoin court");
var rejoinBooked = new ApplicationUser("rejoinbooked", "rejoin-booked@example.test", "test-only", "Booked", "Player", PaymentPlan.DropIn) { EmailConfirmed = true };
var rejoinWaiting = new ApplicationUser("rejoinwaiting", "rejoin-waiting@example.test", "test-only", "Waiting", "Player", PaymentPlan.DropIn) { EmailConfirmed = true };
await using (var db = Db()) { db.Sessions.Add(rejoinSession); db.Users.AddRange(rejoinBooked, rejoinWaiting); await db.SaveChangesAsync(); }
await using (var db = Db()) await new ParticipationRepository(db).ReserveAsync(rejoinSession.Id, rejoinBooked.Id);
await using (var db = Db()) await new ParticipationRepository(db).JoinWaitlistAsync(rejoinSession.Id, rejoinWaiting.Id, null);
await using (var db = Db()) await new ParticipationRepository(db).LeaveWaitlistAsync(rejoinSession.Id, rejoinWaiting.Id);
var rejoined = false;
try { await using var db = Db(); rejoined = (await new ParticipationRepository(db).JoinWaitlistAsync(rejoinSession.Id, rejoinWaiting.Id, "back")).Status == WaitlistStatus.Waiting; }
catch (DbUpdateException) { rejoined = false; }
await using (var db = Db())
    Assert(rejoined && await db.Waitlists.CountAsync(w => w.SessionId == rejoinSession.Id && w.UserId == rejoinWaiting.Id) == 1,
        "a player who left a waitlist can join it again");

// Admin-audit features (each file under Features/).
await OutstandingBalancesChecks.RunAsync(Db, Assert);
await CourtAttendanceChecks.RunAsync(Db, Assert);
await TreasurerReportChecks.RunAsync(Db, Assert);
await PlayerTimelineChecks.RunAsync(Db, Assert);
await SeasonRolloverChecks.RunAsync(Db, Assert);
await WaitlistAdminChecks.RunAsync(Db, Assert);
await PromoReferralReportsChecks.RunAsync(Db, Assert);
await VolunteerRolesChecks.RunAsync(Db, Assert);
await SeasonScheduleWizardChecks.RunAsync(Db, Assert);
await SeasonPlanChoiceChecks.RunAsync(Db, Assert);
await SeasonDashboardChecks.RunAsync(Db, Assert);
await SignupFunnelChecks.RunAsync(Db, Assert);
await InteracAutoMatchChecks.RunAsync(Db, Assert);
await SessionCancellationEmailChecks.RunAsync(Db, Assert);
await BookingConfirmationChecks.RunAsync(Db, Assert);
await PlanChoiceConfirmationEmailChecks.RunAsync(Db, Assert);
await SeasonSpotsAndRosterChecks.RunAsync(Db, Assert);
await TreasurerDropInBySeasonChecks.RunAsync(Db, Assert);
await SeasonPlanChoiceEmailChecks.RunAsync(Db, Assert);
await EmailSetRedesignChecks.RunAsync(Db, Assert);
Console.WriteLine($"Regression checks complete. Isolated database retained: {database}");

sealed class StubSmsHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
{
    public string? ApiKey { get; private set; }
    public string? Body { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ApiKey = request.Headers.TryGetValues("api-key", out var values) ? values.Single() : null;
        Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(status) { Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json") };
    }
}

sealed class RecordingCache : SaintHenriBasketball.Application.Services.Interfaces.ICacheService
{
    public List<string> Removed { get; } = new();
    public Task<T?> GetAsync<T>(string key) => Task.FromResult<T?>(default);
    public Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null) => Task.CompletedTask;
    public Task RemoveAsync(string key) { Removed.Add(key); return Task.CompletedTask; }
    public Task RemoveByPrefixAsync(string prefix) { Removed.Add(prefix + "*"); return Task.CompletedTask; }
}

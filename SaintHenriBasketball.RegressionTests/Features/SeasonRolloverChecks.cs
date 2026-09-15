using System.Globalization;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.DTOs.SeasonRollover;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Regression checks for the `season-rollover` feature. Uses its own data; other checks share the database.
/// Every season and session here is dated 2041 or later so no other check overlaps.
internal static class SeasonRolloverChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["JwtSettings:Key"] = "season-rollover-regression-key-not-used-by-the-running-app", ["AppUrl"] = "http://localhost" }).Build();
        var (emailService, email) = RolloverRecordingEmail.New();

        SeasonRolloverService Rollover(ApplicationDbContext context)
        {
            var cache = new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()), NullLogger<MemoryCacheService>.Instance);
            // Session generation only uses the session repository and the cache.
            var sessions = new SessionService(new SessionRepository(context), null!, null!, null!, null!, NullLogger<SessionService>.Instance, cache, null!, null!);
            return new SeasonRolloverService(new SeasonRepository(context, NullLogger<SeasonRepository>.Instance), new SeasonRolloverRepository(context),
                sessions, new AuditLogService(new AuditLogRepository(context)), new AuditLogRepository(context), emailService,
                new UnsubscribeLinks(config), config, cache, NullLogger<SeasonRolloverService>.Instance);
        }
        async Task<Exception?> FailureAsync(Func<SeasonRolloverService, Task> action)
        {
            try { await using var context = db(); await action(Rollover(context)); return null; }
            catch (Exception ex) { return ex; }
        }
        async Task SetStatusAsync(Guid seasonId, SeasonStatus status)
        {
            await using var context = db();
            await context.Seasons.Where(s => s.Id == seasonId).ExecuteUpdateAsync(u => u.SetProperty(s => s.Status, status));
        }
        static DateTime SaturdayOnOrAfter(DateTime date) { var d = date.Date; while (d.DayOfWeek != DayOfWeek.Saturday) d = d.AddDays(1); return d; }

        // ---- Source season with sessions, plus a season and a session in the way of the next one ----
        var sourceStart = SaturdayOnOrAfter(new DateTime(2041, 3, 1));
        var sourceEnd = sourceStart.AddDays(84); // a Saturday
        var source = new Season(sourceStart, sourceEnd, 125m, $"Bring a light and a dark shirt {tag}") { Name = $"Spring 2041 {tag}", Status = SeasonStatus.Closed };
        var expectedStart = sourceEnd.AddDays(7);
        var overlapping = new Season(expectedStart.AddDays(14), expectedStart.AddDays(30), 40m) { Name = $"Overlap {tag}", Status = SeasonStatus.Closed };
        var court = $"Rollover court {tag}";
        var latestSourceSession = new Session(sourceStart.AddDays(77), 18, 12m, "09:30", "11:30", court);
        var existingOnNewDate = new Session(expectedStart.AddDays(7), 30, 5m, "10:00", "12:00", $"Existing court {tag}");
        await using (var context = db())
        {
            context.Seasons.AddRange(source, overlapping);
            context.Sessions.AddRange(new Session(sourceStart.AddDays(7), 16, 11m, "10:00", "12:00", $"Old court {tag}"), latestSourceSession, existingOnNewDate);
            await context.SaveChangesAsync();
        }

        // ---- Preview ----
        SeasonRolloverPreviewDto preview;
        await using (var context = db()) preview = await Rollover(context).PreviewAsync(source.Id, null);
        var proposed = preview.ProposedSeason;
        assert(proposed.StartDate == expectedStart && proposed.EndDate == expectedStart.AddDays(84) && proposed.StartDate.Kind == DateTimeKind.Utc
            && proposed.Name == $"Spring 2042 {tag}" && proposed.Price == 125m && proposed.Notes == source.Notes && proposed.OverriddenFields.Count == 0,
            "rollover preview starts the Saturday after the source season ends, keeps its length, fee and notes, and moves the year in the name forward");
        assert(preview.Sessions.Count == 13 && preview.Sessions[0].Date == expectedStart
            && preview.Sessions.All(s => s.Date.DayOfWeek == DayOfWeek.Saturday && s.StartTime == "09:30" && s.EndTime == "11:30"
                && s.MaxCapacity == 18 && s.DropInPrice == 12m && s.Location == court && s.StartsAtUtc.Kind == DateTimeKind.Utc)
            && preview.SessionPlan.BasedOnSessionId == latestSourceSession.Id,
            "rollover preview proposes every Saturday with the times, capacity, location and drop-in price of the source season's latest session");
        assert(preview.SessionsToSkip == 1 && preview.SessionsToCreate == 12 && preview.Sessions.Single(s => s.AlreadyExists).Date == existingOnNewDate.SessionDate
            && preview.Conflicts.ExistingSessions.Any(s => s.Id == existingOnNewDate.Id)
            && preview.Conflicts.OverlappingSeasons.Any(s => s.Id == overlapping.Id) && preview.Conflicts.OverlappingSeasons.All(s => s.Id != source.Id)
            && !preview.Conflicts.DuplicateSeason && preview.CanCreate,
            "rollover preview lists overlapping seasons and sessions already on the proposed dates");

        var customStart = expectedStart.AddDays(28);
        SeasonRolloverPreviewDto custom, startOnly;
        await using (var context = db())
        {
            custom = await Rollover(context).PreviewAsync(source.Id, new SeasonRolloverRequestDto { Name = $"  Custom {tag} ", StartDate = customStart, EndDate = customStart.AddDays(27), Price = 99.5m });
            startOnly = await Rollover(context).PreviewAsync(source.Id, new SeasonRolloverRequestDto { StartDate = customStart });
        }
        assert(custom.ProposedSeason.Name == $"Custom {tag}" && custom.ProposedSeason.Price == 99.5m && custom.ProposedSeason.StartDate == customStart
            && custom.ProposedSeason.EndDate == customStart.AddDays(27) && custom.ProposedSeason.OverriddenFields.Count == 4
            && custom.Sessions.Count == 4 && custom.Sessions[0].Date == customStart && custom.SessionsToSkip == 0
            && custom.Conflicts.OverlappingSeasons.Any(s => s.Id == overlapping.Id)
            && startOnly.ProposedSeason.EndDate == customStart.AddDays(84) && startOnly.ProposedSeason.Name == $"Spring 2042 {tag}",
            "rollover preview reflects name, date and fee overrides, and a start date alone keeps the source season's length");
        var backwards = await FailureAsync(s => s.PreviewAsync(source.Id, new SeasonRolloverRequestDto { StartDate = customStart, EndDate = customStart.AddDays(-1) }));
        var negativeFee = await FailureAsync(s => s.PreviewAsync(source.Id, new SeasonRolloverRequestDto { Price = -1m }));
        var unknownSource = await FailureAsync(s => s.PreviewAsync(Guid.NewGuid(), null));
        assert(backwards is ValidationException && negativeFee is ValidationException && unknownSource is NotFoundException
            && SeasonRolloverService.NextName("Saison 2025-2026", new DateTime(2025, 9, 6), new DateTime(2026, 9, 5)) == "Saison 2026-2027"
            && SeasonRolloverService.NextName("Saturday runs", new DateTime(2025, 9, 6), new DateTime(2026, 1, 3)) == "Saturday runs",
            "rollover refuses backwards dates, a negative fee and an unknown season, and only renames seasons that carry a year");

        // ---- Draft ----
        SeasonRolloverDraftResultDto draft;
        await using (var context = db()) draft = await Rollover(context).CreateDraftAsync(source.Id, null, null, "Regression");
        await using (var context = db())
        {
            var created = await context.Seasons.AsNoTracking().SingleAsync(s => s.Id == draft.SeasonId);
            var current = await new SeasonRepository(context, NullLogger<SeasonRepository>.Instance).GetCurrentSeasonAsync();
            var newSessions = await context.Sessions.AsNoTracking().Where(s => s.Location == court && s.SessionDate >= expectedStart).ToListAsync();
            assert(created.Status == SeasonStatus.Closed && current?.Id != created.Id && created.Name == $"Spring 2042 {tag}"
                && created.StartDate == expectedStart && created.EndDate == expectedStart.AddDays(84) && created.Price == 125m && created.Notes == source.Notes
                && draft.Status == SeasonStatus.Closed && draft.StartDate.Kind == DateTimeKind.Utc,
                "creating the rollover draft adds the proposed season without making it the current season");
            assert(draft.SessionsCreated == 12 && draft.SessionsSkipped == 1 && draft.SkippedDates.Single() == existingOnNewDate.SessionDate
                && newSessions.Count == 12 && newSessions.All(s => s.SessionDate.DayOfWeek == DayOfWeek.Saturday && s.StartTime == "09:30" && s.MaxCapacity == 18 && s.DropInPrice == 12m)
                && newSessions.All(s => s.SessionDate != existingOnNewDate.SessionDate),
                "creating the rollover draft adds its Saturday sessions and skips dates that already have one");
        }
        var duplicate = await FailureAsync(s => s.CreateDraftAsync(source.Id, null, null, "Regression"));
        SeasonRolloverPreviewDto afterDraft;
        await using (var context = db()) afterDraft = await Rollover(context).PreviewAsync(source.Id, null);
        await using (var context = db())
            assert(duplicate is ValidationException && afterDraft.Conflicts.DuplicateSeason && !afterDraft.CanCreate
                && await context.Seasons.CountAsync(s => s.Name == $"Spring 2042 {tag}") == 1
                && await context.AuditLogs.CountAsync(a => a.EntityId == draft.SeasonId && a.Action == SeasonRolloverService.DraftCreatedAction) == 1,
                "a second rollover to the same dates is refused and the preview flags the duplicate");

        // ---- Invitations: players who paid for the source season ----
        ApplicationUser Player(string key, EmailLanguage language = EmailLanguage.English, PaymentPlan plan = PaymentPlan.DropIn) =>
            new($"ro_{key}_{tag}", $"ro-{key}-{tag}@example.test", "test-only", "Rollover", key, plan) { EmailConfirmed = true, PreferredLanguage = language };
        var english = Player("english");
        var french = Player("french", EmailLanguage.French);
        var deactivated = Player("deactivated");
        deactivated.IsDeactivated = true;
        var noCommunity = Player("nocommunity");
        noCommunity.CommunityUpdatesEnabled = false;
        var noEmail = Player("noemail");
        noEmail.EmailNotificationsEnabled = false;
        var renewed = Player("renewed");
        var failing = Player("fail");
        var pendingOnly = Player("pending");
        var planOnly = Player("planonly", plan: PaymentPlan.Season);
        await using (var context = db())
        {
            context.Users.AddRange(english, french, deactivated, noCommunity, noEmail, renewed, failing, pendingOnly, planOnly);
            foreach (var paid in new[] { english, french, deactivated, noCommunity, noEmail, renewed, failing })
                context.Payments.Add(new Payment(paid.Id, 125m, PaymentPlan.Season) { SeasonId = source.Id, Status = PaymentStatus.Completed });
            context.Payments.Add(new Payment(pendingOnly.Id, 125m, PaymentPlan.Season) { SeasonId = source.Id });
            context.Payments.Add(new Payment(renewed.Id, 125m, PaymentPlan.Season) { SeasonId = draft.SeasonId });
            await context.SaveChangesAsync();
        }

        var closedRefused = await FailureAsync(s => s.SendRenewalInvitesAsync(draft.SeasonId, new SeasonRenewalInviteRequestDto { SourceSeasonId = source.Id }, null, "Regression"));
        assert(closedRefused is ValidationException { Message: SeasonRolloverService.NewSeasonClosedMessage } && email.Sent.Count == 0,
            "renewal invites are refused while the new season is still closed");
        // Open the draft only around the invite checks: other checks treat the Open season as the current one.
        await SetStatusAsync(draft.SeasonId, SeasonStatus.Open);
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var context = db();
            return await Rollover(context).SendRenewalInvitesAsync(draft.SeasonId, new SeasonRenewalInviteRequestDto { SourceSeasonId = source.Id }, null, "Regression");
        }));
        assert(concurrent.Count(r => r.AlreadySent) == 1, "two simultaneous invite requests for the same season send only once");
        var first = concurrent.Single(r => !r.AlreadySent);
        string? ReasonFor(SeasonRenewalInviteResultDto result, ApplicationUser user) => result.SkippedPlayers.SingleOrDefault(p => p.UserId == user.Id)?.Reason;
        assert(first is { RecipientRule: SeasonRolloverService.RuleCompletedPayments, Candidates: 7, Sent: 2, Skipped: 4, Failed: 1, AlreadySent: false }
            && ReasonFor(first, deactivated) == SeasonRolloverService.SkipDeactivated && ReasonFor(first, noCommunity) == SeasonRolloverService.SkipCommunityUpdatesOff
            && ReasonFor(first, noEmail) == SeasonRolloverService.SkipEmailNotificationsOff && ReasonFor(first, renewed) == SeasonRolloverService.SkipAlreadyRenewed
            && first.FailedPlayers.Single().UserId == failing.Id && first.SkippedReasons.Sum(r => r.Count) == 4
            && email.Sent.Select(s => s.To).OrderBy(t => t).SequenceEqual(new[] { english.Email!, french.Email! }.OrderBy(t => t))
            && !email.Sent.Any(s => s.To == pendingOnly.Email || s.To == planOnly.Email),
            "renewal invites go to players with a completed payment for the source season, skipping deactivated, opted-out and already renewed players");
        var englishMail = email.Sent.Single(s => s.To == english.Email);
        var frenchMail = email.Sent.Single(s => s.To == french.Email);
        assert(englishMail.Subject == $"Renew for Spring 2042 {tag}" && englishMail.Html.Contains(expectedStart.ToString("MMMM d, yyyy", new CultureInfo("en-CA")))
            && englishMail.Html.Contains("$125.00") && englishMail.Html.Contains("href='http://localhost'") && englishMail.Html.Contains("http://localhost/unsubscribe?token=")
            && englishMail.Html.Contains("9:30–11:30")
            && frenchMail.Subject == $"Renouvelez pour Spring 2042 {tag}" && frenchMail.Html.Contains("125,00") && frenchMail.Html.Contains("Se désabonner"),
            "renewal invites are written in the player's language with the season dates, fee, app link and unsubscribe link");

        SeasonRenewalInviteResultDto repeat, resent;
        await using (var context = db()) repeat = await Rollover(context).SendRenewalInvitesAsync(draft.SeasonId, new SeasonRenewalInviteRequestDto { SourceSeasonId = source.Id }, null, "Regression");
        var sendsAfterRepeat = email.Sent.Count;
        await using (var context = db()) resent = await Rollover(context).SendRenewalInvitesAsync(draft.SeasonId, new SeasonRenewalInviteRequestDto { SourceSeasonId = source.Id, Resend = true }, null, "Regression");
        await using (var context = db())
            assert(repeat is { AlreadySent: true, Sent: 0, Failed: 0 } && repeat.PreviouslySentAt?.Kind == DateTimeKind.Utc && sendsAfterRepeat == 2
                && resent is { AlreadySent: false, Sent: 2, Failed: 1 } && email.Sent.Count == 4
                && await context.AuditLogs.CountAsync(a => a.EntityId == draft.SeasonId && a.Action == SeasonRolloverService.InvitesSentAction) == 2,
                "renewal invites are not sent twice for the same season unless resend is requested, and each send is audited");
        var sameSeason = await FailureAsync(s => s.SendRenewalInvitesAsync(draft.SeasonId, new SeasonRenewalInviteRequestDto { SourceSeasonId = draft.SeasonId }, null, "Regression"));
        var unknownNew = await FailureAsync(s => s.SendRenewalInvitesAsync(Guid.NewGuid(), new SeasonRenewalInviteRequestDto { SourceSeasonId = source.Id }, null, "Regression"));
        assert(sameSeason is ValidationException && unknownNew is NotFoundException, "renewal invites need a different, existing source season");
        await SetStatusAsync(draft.SeasonId, SeasonStatus.Closed);

        // ---- Invitations: a season with no linked payments falls back to Season-plan players ----
        var legacyStart = SaturdayOnOrAfter(new DateTime(2044, 9, 1));
        var legacy = new Season(legacyStart, legacyStart.AddDays(90), 110m) { Name = $"Fall 2044 {tag}", Status = SeasonStatus.Closed };
        var planPlayer = Player("plan", plan: PaymentPlan.Season);
        var planOptedOut = Player("planoff", plan: PaymentPlan.Season);
        planOptedOut.CommunityUpdatesEnabled = false;
        var dropInPending = Player("droppending");
        await using (var context = db())
        {
            context.Seasons.Add(legacy);
            context.Users.AddRange(planPlayer, planOptedOut, dropInPending);
            context.Payments.Add(new Payment(dropInPending.Id, 110m, PaymentPlan.Season) { SeasonId = legacy.Id });
            await context.SaveChangesAsync();
        }
        SeasonRolloverPreviewDto legacyPreview;
        SeasonRolloverDraftResultDto legacyDraft;
        SeasonRenewalInviteResultDto fallback;
        await using (var context = db())
        {
            legacyPreview = await Rollover(context).PreviewAsync(legacy.Id, null);
            legacyDraft = await Rollover(context).CreateDraftAsync(legacy.Id, null, null, "Regression");
        }
        await SetStatusAsync(legacyDraft.SeasonId, SeasonStatus.Open);
        await using (var context = db()) fallback = await Rollover(context).SendRenewalInvitesAsync(legacyDraft.SeasonId, new SeasonRenewalInviteRequestDto { SourceSeasonId = legacy.Id }, null, "Regression");
        await SetStatusAsync(legacyDraft.SeasonId, SeasonStatus.Closed);
        assert(legacyPreview.ProposedSeason.StartDate == SaturdayOnOrAfter(legacy.EndDate.AddDays(1)) && legacyPreview.ProposedSeason.EndDate == legacyPreview.ProposedSeason.StartDate.AddDays(90)
            && legacyPreview.ProposedSeason.Name == $"Fall 2045 {tag}"
            && legacyPreview.SessionPlan is { StartTime: "10:00", EndTime: "12:00", BasedOnSessionId: null },
            "a source season ending mid-week rolls over to the next Saturday, and one without sessions proposes the 10:00-12:00 default");
        assert(fallback.RecipientRule == SeasonRolloverService.RuleSeasonPlanPlayers
            && email.Sent.Any(s => s.To == planPlayer.Email) && ReasonFor(fallback, planOptedOut) == SeasonRolloverService.SkipCommunityUpdatesOff
            && !email.Sent.Any(s => s.To == dropInPending.Email || s.To == planOptedOut.Email),
            "renewal invites fall back to Season-plan players when no completed payment is linked to the source season");
    }
}

/// Records SendEmailAsync calls; an address containing "-fail-" simulates a delivery failure. Nothing else is expected.
public class RolloverRecordingEmail : DispatchProxy
{
    public List<(string To, string Subject, string Html)> Sent { get; } = new();

    public static (IEmailService Service, RolloverRecordingEmail Recorder) New()
    {
        var proxy = Create<IEmailService, RolloverRecordingEmail>();
        return (proxy, (RolloverRecordingEmail)(object)proxy);
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name != nameof(IEmailService.SendEmailAsync))
            throw new NotSupportedException($"Season rollover should only call SendEmailAsync, not {targetMethod?.Name}");
        var to = (string?)args![0] ?? string.Empty;
        if (to.Contains("-fail-")) return Task.FromException(new InvalidOperationException("Simulated send failure"));
        Sent.Add((to, (string)args[1]!, (string)args[2]!));
        return Task.CompletedTask;
    }
}

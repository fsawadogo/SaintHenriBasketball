using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Application.DTOs.VolunteerRoles;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Mapping;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Regression checks for the `volunteer-roles` feature. Uses its own data; other checks share the database.
internal static class VolunteerRolesChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var tag = Guid.NewGuid().ToString("N")[..6];

        // ---- Access helper: admin / staff claims x required role x flag ----
        var claims = new string?[] { null, "", "CourtCaptain", "Treasurer", "treasurer", "None", "Admin", "2" };
        var table = true;
        foreach (var isAdmin in new[] { true, false })
        foreach (var claim in claims)
        foreach (var required in new[] { StaffRole.CourtCaptain, StaffRole.Treasurer })
        foreach (var flag in new[] { true, false })
        {
            var expected = isAdmin || (flag && string.Equals(claim, required.ToString(), StringComparison.OrdinalIgnoreCase));
            if (StaffAccess.IsAllowed(isAdmin, claim, required, flag) != expected) table = false;
        }
        assert(table, "volunteer roles: admins always pass; staff pass only with the matching role while the flag is on");
        assert(StaffAccess.IsAllowed(false, "CourtCaptain", StaffRole.CourtCaptain, true)
            && !StaffAccess.IsAllowed(false, "CourtCaptain", StaffRole.Treasurer, true)
            && !StaffAccess.IsAllowed(false, "Treasurer", StaffRole.CourtCaptain, true)
            && !StaffAccess.IsAllowed(false, "CourtCaptain", StaffRole.CourtCaptain, false)
            && !StaffAccess.IsAllowed(false, "None", StaffRole.None, true)
            && StaffAccess.IsAllowed(true, null, StaffRole.Treasurer, false),
            "volunteer roles: a captain can't use treasurer endpoints, and staff roles grant nothing while the flag is off");
        assert(StaffAccess.ClaimValue(StaffRole.None) == null && StaffAccess.ClaimValue(StaffRole.Treasurer) == "Treasurer"
            && StaffAccess.ClientFlagKeys(StaffRole.None).Count == 0
            && StaffAccess.ClientFlagKeys(StaffRole.CourtCaptain).SequenceEqual(new[] { FeatureFlagKeys.VolunteerRoles, FeatureFlagKeys.CourtAttendance })
            && new[] { FeatureFlagKeys.VolunteerRoles, FeatureFlagKeys.TreasurerReport, FeatureFlagKeys.OutstandingBalances, FeatureFlagKeys.InteracReconciliation, FeatureFlagKeys.PromoReferralReports }
                .All(StaffAccess.ClientFlagKeys(StaffRole.Treasurer).Contains)
            && !StaffAccess.ClientFlagKeys(StaffRole.Treasurer).Contains(FeatureFlagKeys.CourtAttendance),
            "volunteer roles: no claim for None, and each role only receives the flags its screens need");

        // ---- Tokens: a staff role that no longer matches the account is refused ----
        AuthUserSnapshot Snap(StaffRole role, bool admin = false, bool deactivated = false) => new(deactivated, admin, role);
        assert(TokenUserCheck.Evaluate(Snap(StaffRole.None), false, null) == null
            && TokenUserCheck.Evaluate(Snap(StaffRole.Treasurer), false, "Treasurer") == null
            && TokenUserCheck.Evaluate(Snap(StaffRole.CourtCaptain), false, "CourtCaptain") == null
            && TokenUserCheck.Evaluate(Snap(StaffRole.CourtCaptain, admin: true), true, "CourtCaptain") == null,
            "volunteer roles: a token whose staff role matches the account keeps working");
        assert(TokenUserCheck.Evaluate(Snap(StaffRole.None), false, "Treasurer") == "Staff role changed"
            && TokenUserCheck.Evaluate(Snap(StaffRole.CourtCaptain), false, "Treasurer") == "Staff role changed"
            && TokenUserCheck.Evaluate(Snap(StaffRole.Treasurer), false, null) == "Staff role changed"
            && TokenUserCheck.Evaluate(Snap(StaffRole.None), false, "Boss") == "Staff role changed"
            && TokenUserCheck.Evaluate(Snap(StaffRole.Treasurer, deactivated: true), false, "Treasurer") == "Account deactivated"
            && TokenUserCheck.Evaluate(Snap(StaffRole.None), true, "Treasurer") == "Admin access was removed",
            "volunteer roles: a token is refused on its next request once its staff role was removed or changed");

        // ---- Setting a role ----
        assert(StaffAccess.SetRoleRefusal(true, false, StaffRole.Treasurer) == StaffAccess.AdminRefusedMessage
            && StaffAccess.SetRoleRefusal(false, true, StaffRole.CourtCaptain) == StaffAccess.DeactivatedRefusedMessage
            && StaffAccess.SetRoleRefusal(true, true, StaffRole.None) == null
            && StaffAccess.SetRoleRefusal(false, false, StaffRole.Treasurer) == null
            && !StaffAccess.TryParseRole("2", out _) && !StaffAccess.TryParseRole("Boss", out _) && !StaffAccess.TryParseRole(null, out _)
            && StaffAccess.TryParseRole(" courtcaptain ", out var parsed) && parsed == StaffRole.CourtCaptain,
            "volunteer roles: role rules refuse admins, deactivated players, numbers and unknown names");

        ApplicationUser Player(string name) =>
            new($"vr_{name}_{tag}", $"vr-{name}-{tag}@example.test", "test-only", "Volunteer", $"{name} {tag}", PaymentPlan.DropIn) { EmailConfirmed = true };
        var volunteer = Player("volunteer");
        var admin = Player("admin");
        admin.IsAdmin = true;
        var gone = Player("gone");
        gone.IsDeactivated = true;
        gone.StaffRole = StaffRole.CourtCaptain;
        await using (var ctx = db())
        {
            ctx.Users.AddRange(volunteer, admin, gone);
            await ctx.SaveChangesAsync();
        }
        async Task<(StaffRoleChange? Change, Exception? Error)> SetAsync(Guid userId, string? role)
        {
            await using var ctx = db();
            try { return (await new StaffRoleService(new VolunteerRolesRepository(ctx)).SetStaffRoleAsync(userId, role), null); }
            catch (Exception ex) when (ex is ValidationException or NotFoundException) { return (null, ex); }
        }
        async Task<StaffRole> StoredAsync(Guid userId)
        {
            await using var ctx = db();
            return (await ctx.Users.AsNoTracking().SingleAsync(u => u.Id == userId)).StaffRole;
        }

        await using (var ctx = db())
            assert((await ctx.Users.AsNoTracking().SingleAsync(u => u.Id == admin.Id)).StaffRole == StaffRole.None,
                "volunteer roles: new accounts have no volunteer role");
        var granted = await SetAsync(volunteer.Id, "Treasurer");
        var again = await SetAsync(volunteer.Id, " treasurer ");
        assert(granted.Change is { Before: StaffRole.None, After: StaffRole.Treasurer, Changed: true } && again.Change is { Changed: false }
            && await StoredAsync(volunteer.Id) == StaffRole.Treasurer,
            "volunteer roles: an admin gives a player a role, and setting the same role again changes nothing");
        var switched = await SetAsync(volunteer.Id, "CourtCaptain");
        assert(switched.Change is { Before: StaffRole.Treasurer, After: StaffRole.CourtCaptain } && await StoredAsync(volunteer.Id) == StaffRole.CourtCaptain,
            "volunteer roles: a role can be changed to another role");

        var adminRefused = await SetAsync(admin.Id, "CourtCaptain");
        assert(adminRefused.Error is ValidationException { Message: StaffAccess.AdminRefusedMessage } && await StoredAsync(admin.Id) == StaffRole.None,
            "volunteer roles: admins are refused a role because they already have full access");
        var goneRefused = await SetAsync(gone.Id, "Treasurer");
        assert(goneRefused.Error is ValidationException { Message: StaffAccess.DeactivatedRefusedMessage } && await StoredAsync(gone.Id) == StaffRole.CourtCaptain,
            "volunteer roles: deactivated players are refused a role");
        var unknown = await SetAsync(volunteer.Id, "Boss");
        var numeric = await SetAsync(volunteer.Id, "2");
        var empty = await SetAsync(volunteer.Id, null);
        assert(new[] { unknown, numeric, empty }.All(r => r.Error is ValidationException { Message: StaffAccess.UnknownRoleMessage })
            && await StoredAsync(volunteer.Id) == StaffRole.CourtCaptain,
            "volunteer roles: unknown roles are refused");
        var missing = await SetAsync(Guid.NewGuid(), "Treasurer");
        assert(missing.Error is NotFoundException, "volunteer roles: an unknown player is not found");
        var cleared = await SetAsync(gone.Id, "None");
        assert(cleared.Change is { After: StaffRole.None, Changed: true } && await StoredAsync(gone.Id) == StaffRole.None,
            "volunteer roles: a deactivated player's role can still be cleared");

        // ---- Token claim and DTOs ----
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["JwtSettings:Key"] = "local-regression-signing-key-long-enough-for-any-hmac-algorithm-0123456789abcdef",
            ["JwtSettings:Issuer"] = "regression", ["JwtSettings:Audience"] = "regression", ["JwtSettings:DurationInDays"] = "1",
            ["AppUrl"] = "http://localhost" }).Build();
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        async Task<string?> StaffClaimAsync(Guid userId)
        {
            await using var ctx = db();
            var flags = new FeatureFlagService(new FeatureFlagRepository(ctx),
                new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()), NullLogger<MemoryCacheService>.Instance),
                new AuditLogRepository(ctx), NullLogger<FeatureFlagService>.Instance);
            var users = new UserService(config, mapper, new UserRepository(ctx, NullLogger<UserRepository>.Instance), null!,
                NullLogger<UserService>.Instance, flags, new ReferralRepository(ctx));
            var token = await users.IssueTokenAsync(userId);
            return new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.FirstOrDefault(c => c.Type == StaffAccess.ClaimType)?.Value;
        }
        assert(await StaffClaimAsync(volunteer.Id) == "CourtCaptain" && await StaffClaimAsync(admin.Id) == null,
            "volunteer roles: tokens carry the staff_role claim only when the player has a role");
        await using (var ctx = db())
        {
            var dto = mapper.Map<UserDto>(await ctx.Users.AsNoTracking().SingleAsync(u => u.Id == volunteer.Id));
            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var login = JsonSerializer.Serialize(new UserResponseDto { Token = "t", Username = "u", Email = "e", FirstName = "f", LastName = "l", StaffRole = StaffRole.Treasurer },
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            assert(dto.StaffRole == StaffRole.CourtCaptain && json.Contains("\"staffRole\":\"CourtCaptain\"") && login.Contains("\"staffRole\":\"Treasurer\""),
                "volunteer roles: user and login responses expose staffRole by name");
        }

        // ---- Audit policy ----
        var none = new string?[] { null };
        assert(AdminAuditPolicy.RequiresAdminLevel(none, new string?[] { StaffAccess.CourtCaptainOrAdminPolicy })
            && AdminAuditPolicy.RequiresAdminLevel(new string?[] { null, null }, new string?[] { null, StaffAccess.TreasurerOrAdminPolicy })
            && AdminAuditPolicy.RequiresAdminLevel(new string?[] { "Admin" }, none)
            && !AdminAuditPolicy.RequiresAdminLevel(none, none)
            && !AdminAuditPolicy.RequiresAdminLevel(new string?[] { "User" }, new string?[] { "SomeOtherPolicy" })
            && AdminAuditPolicy.ShouldAudit("POST", AdminAuditPolicy.RequiresAdminLevel(none, new string?[] { StaffAccess.TreasurerOrAdminPolicy }), 200),
            "volunteer roles: changes behind staff policies are audited as admin-level changes");

        // ---- Court captain: sessions to pick ----
        var today = SessionTimeHelper.ToLocal(DateTime.UtcNow).Date;
        var (from, to) = CourtCaptainService.SessionWindow(DateTime.UtcNow);
        assert(from == today.AddDays(-7) && to == today.AddDays(14), "court sessions: the window runs from 7 days ago to 14 days ahead");
        var place = $"VR court {tag}";
        Session At(int days, string start = "10:00") => new(today.AddDays(days), 12, 10m, start, "12:00", place);
        var tooOld = At(-8);
        var oldest = At(-7);
        var eveningToday = At(0, "18:00");
        var morningToday = At(0, "09:00");
        var furthest = At(14);
        var tooFar = At(15);
        var cancelled = At(1);
        cancelled.Status = SessionStatus.Cancelled;
        await using (var ctx = db())
        {
            ctx.Sessions.AddRange(tooOld, oldest, eveningToday, morningToday, furthest, tooFar, cancelled);
            await ctx.SaveChangesAsync();
        }
        await using (var ctx = db())
        {
            var listed = (await new CourtCaptainService(new VolunteerRolesRepository(ctx)).GetSessionsAsync()).Where(s => s.Location == place).ToList();
            assert(listed.Select(s => s.Id).SequenceEqual(new[] { oldest.Id, morningToday.Id, eveningToday.Id, furthest.Id })
                && listed[0].SessionDate == today.AddDays(-7).ToString("yyyy-MM-dd") && listed[0].Capacity == 12 && listed[0].Registered == 0
                && listed[0].StartTime == "10:00" && listed[0].EndTime == "12:00",
                "court sessions: recent and upcoming sessions, not cancelled, by date and start time");
        }

        // ---- Court captain: walk-in player search ----
        var searchTag = $"Zq{tag}";
        var matches = Enumerable.Range(0, 22)
            .Select(i => new ApplicationUser($"vrs_{i}_{tag}", $"vrs-{i}-{tag}@example.test", "test-only", "Walk", $"{searchTag} {i:00}", PaymentPlan.DropIn) { EmailConfirmed = true })
            .ToList();
        // Sorts among the first results, so it would show up if deactivated players weren't excluded.
        var hidden = new ApplicationUser($"vrs_gone_{tag}", $"vrs-gone-{tag}@example.test", "test-only", "Walk", $"{searchTag} 00a", PaymentPlan.DropIn) { EmailConfirmed = true, IsDeactivated = true };
        await using (var ctx = db())
        {
            ctx.Users.AddRange(matches);
            ctx.Users.Add(hidden);
            await ctx.SaveChangesAsync();
        }
        await using (var ctx = db())
        {
            var service = new CourtCaptainService(new VolunteerRolesRepository(ctx));
            var found = await service.SearchPlayersAsync($"  {searchTag} ");
            var byEmail = await service.SearchPlayersAsync($"vrs-3-{tag}@");
            var tooShort = await service.SearchPlayersAsync("z");
            assert(found.Count == CourtCaptainService.MaxPlayers && found.All(p => p.Name.Contains(searchTag)) && found.All(p => p.Id != hidden.Id)
                && found[0].Id == matches[0].Id
                && byEmail.Count == 1 && byEmail[0].Id == matches[3].Id && byEmail[0].Email == matches[3].Email && byEmail[0].Name == $"Walk {searchTag} 03"
                && tooShort.Count == 0,
                "court players: active players only, at most 20, with at least 2 characters to search");
        }
    }
}

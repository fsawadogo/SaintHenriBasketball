using Microsoft.Extensions.Caching.Memory;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.API.Filters;

/// Per-user snapshot for token validation, cached briefly so each request doesn't hit the database.
/// Call <see cref="Forget"/> after deactivating, reactivating or changing a user's admin access or volunteer role.
public static class AuthUserCache
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private static string Key(Guid userId) => $"auth-user:{userId}";

    public static async Task<AuthUserSnapshot?> GetAsync(IMemoryCache cache, IUserRepository users, Guid userId)
    {
        if (cache.TryGetValue(Key(userId), out AuthUserSnapshot? snapshot)) return snapshot;
        var user = await users.GetByIdAsync(userId);
        snapshot = user is null ? null : new AuthUserSnapshot(user.IsDeactivated, user.IsAdmin, user.StaffRole, user.EmailConfirmed);
        cache.Set(Key(userId), snapshot, Lifetime);
        return snapshot;
    }

    public static void Forget(IMemoryCache cache, Guid userId) => cache.Remove(Key(userId));
}

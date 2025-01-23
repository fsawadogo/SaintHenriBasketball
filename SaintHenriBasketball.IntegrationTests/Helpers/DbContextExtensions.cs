using Microsoft.EntityFrameworkCore;

namespace SaintHenriBasketball.IntegrationTests.Helpers;

public static class DbContextExtensions
{
    public static async Task ClearTableAsync<T>(this DbContext context) where T : class
    {
        var dbSet = context.Set<T>();
        if (await dbSet.AnyAsync())
        {
            dbSet.RemoveRange(await dbSet.ToListAsync());
            await context.SaveChangesAsync();
        }
    }
}

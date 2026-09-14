using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

internal static class DbUpdateExceptions
{
    /// SQL Server 2601 (unique index) / 2627 (unique constraint).
    public static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    /// A failed insert stays tracked as Added and would be retried by the next SaveChanges in the scope.
    public static void Detach(DbContext context, params object?[] entities)
    {
        foreach (var entity in entities)
            if (entity is not null)
                context.Entry(entity).State = EntityState.Detached;
    }
}

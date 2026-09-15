using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface IBroadcastRepository
{
    Task AddAsync(BroadcastMessage message);
    Task<BroadcastMessage?> GetByIdAsync(Guid id);

    /// Newest first, with the total number of broadcasts.
    Task<(IReadOnlyList<BroadcastMessage> Items, int Total)> GetPageAsync(int page, int pageSize);

    Task RecordDeliveryAsync(Guid id, BroadcastStatus status, int attempted, int succeeded, int failed);

    /// Marks a broadcast that never finished delivering as failed; one already recorded as sent is left alone.
    Task MarkFailedAsync(Guid id);
}

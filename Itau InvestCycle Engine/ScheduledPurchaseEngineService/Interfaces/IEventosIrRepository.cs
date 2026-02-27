using ClassLibrary.Domain.Entities.RebalanceamentoIR;

namespace ScheduledPurchaseEngineService.Interfaces;

public interface IEventosIrRepository
{
    Task AddManyAsync(IReadOnlyList<EventosIR> events, CancellationToken ct = default);
    Task MarkPublishedAsync(IReadOnlyList<long> eventIds, DateTimeOffset publishedAtUtc, CancellationToken ct = default);
}

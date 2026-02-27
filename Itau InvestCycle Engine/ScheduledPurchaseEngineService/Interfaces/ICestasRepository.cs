using ClassLibrary.Domain.Entities.Cestas;

namespace ScheduledPurchaseEngineService.Interfaces
{
    public interface ICestasRepository
    {
        Task<CestasRecomendacao?> GetActiveAsync(CancellationToken ct = default);
        Task<IReadOnlyList<ItensCesta>> GetItensAsync(long cestaId, CancellationToken ct = default);
        Task AddAsync(CestasRecomendacao cesta, IReadOnlyList<ItensCesta> itens, CancellationToken ct = default);
        Task DeactivateAsync(long cestaId, DateTimeOffset deactivatedAtUtc, CancellationToken ct = default);
    }
}

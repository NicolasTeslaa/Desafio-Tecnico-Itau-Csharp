using ClassLibrary.Domain.Entities.CompraDistribuicao;

namespace ScheduledPurchaseEngineService.Interfaces
{
    public interface IOrdensCompraRepository
    {
        Task<bool> ExistsForExecutionDateAsync(DateOnly execDate, CancellationToken ct = default); // idempotency
        Task AddManyAsync(IReadOnlyList<OrdensCompra> orders, CancellationToken ct = default);
    }
}

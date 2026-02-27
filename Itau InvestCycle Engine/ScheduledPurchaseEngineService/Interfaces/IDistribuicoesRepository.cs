using ClassLibrary.Domain.Entities.CompraDistribuicao;

namespace ScheduledPurchaseEngineService.Interfaces
{
    public interface IDistribuicoesRepository
    {
        Task AddManyAsync(IReadOnlyList<Distribuicoes> distribs, CancellationToken ct = default);
    }
}

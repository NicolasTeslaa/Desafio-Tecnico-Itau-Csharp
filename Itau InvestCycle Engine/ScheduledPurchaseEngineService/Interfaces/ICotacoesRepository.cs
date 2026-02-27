using ClassLibrary.Domain.Entities;

namespace ScheduledPurchaseEngineService.Interfaces
{
    public interface ICotacoesRepository
    {
        Task<Cotacoes?> GetByDateAndTickerAsync(DateOnly tradeDate, string ticker, CancellationToken ct = default);
        Task AddManyAsync(IReadOnlyList<Cotacoes> items, CancellationToken ct = default);
    }
}

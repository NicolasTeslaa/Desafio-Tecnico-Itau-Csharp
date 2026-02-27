using ClassLibrary.Domain.Entities.CompraDistribuicao;

namespace ScheduledPurchaseEngineService.Interfaces
{
    public interface ICustodiasRepository
    {
        Task<IReadOnlyList<Custodias>> ListByContaGraficaIdAsync(long contaGraficaId, CancellationToken ct = default);
        
        // upsert position (buy path updates quantity + average price)
        Task UpsertBuyAsync(long contaGraficaId, string ticker, int quantity, decimal unitPrice, DateOnly execDate, CancellationToken ct = default);

        // optional for later rebalance: sell decreases quantity, avg price unchanged (RN-043) :contentReference[oaicite:6]{index=6}
        Task ApplySellAsync(long contaGraficaId, string ticker, int quantity, decimal unitPrice, DateOnly execDate, CancellationToken ct = default);
    }
}

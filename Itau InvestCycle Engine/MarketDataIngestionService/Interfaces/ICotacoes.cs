using ClassLibrary.Contracts.DTOs;
using Itau.InvestCycleEngine.Contracts.Common;

namespace MarketDataIngestionService.Interfaces;

public interface ICotacoesService
{
    Task<int> SaveFromCotahistAsync(IEnumerable<CotahistPriceRecord> records, CancellationToken ct);
    Task<CotacaoIngestDto?> GetByIdAsync(int id, CancellationToken ct);
    Task<PagedResponse<CotacaoIngestDto>> ListAsync(PagedRequest request, string? ticker, DateTime? dataPregao, CancellationToken ct);
    Task<IReadOnlyList<string>> ListDistinctTickersAsync(string? query, int limit, CancellationToken ct);
}

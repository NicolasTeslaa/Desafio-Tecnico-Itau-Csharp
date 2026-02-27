using ClassLibrary.Contracts.DTOs;
using ClassLibrary.Domain.Entities;
using Itau.InvestCycleEngine.Contracts.Common;

namespace MarketDataIngestionService.Interfaces;

public interface ICotacoesRepository
{
    Task UpsertBatchAsync(IEnumerable<Cotacoes> items, CancellationToken ct);
    Task<CotacaoIngestDto?> GetByIdAsync(int id, CancellationToken ct);
    Task<PagedResponse<CotacaoIngestDto>> ListAsync(PagedRequest request, string? ticker, DateTime? dataPregao, CancellationToken ct);
}

public interface ICotacoesService
{
    Task<int> SaveAsync(IEnumerable<CotacaoIngestDto> dtos, CancellationToken ct);
    Task<int> SaveFromCotahistAsync(IEnumerable<CotahistPriceRecord> records, CancellationToken ct);
    Task<CotacaoIngestDto?> GetByIdAsync(int id, CancellationToken ct);
    Task<PagedResponse<CotacaoIngestDto>> ListAsync(PagedRequest request, string? ticker, DateTime? dataPregao, CancellationToken ct);
}

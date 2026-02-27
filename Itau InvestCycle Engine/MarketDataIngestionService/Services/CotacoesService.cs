using ClassLibrary.Contracts.DTOs;
using ClassLibrary.Domain.Entities;
using Itau.InvestCycleEngine.Contracts.Common;
using MarketDataIngestionService.Interfaces;

namespace MarketDataIngestionService.Services;

public sealed class CotacoesService : ICotacoesService
{
    private readonly ICotacoesRepository _repo;

    public CotacoesService(ICotacoesRepository repo)
    {
        _repo = repo;
    }

    public async Task<int> SaveFromCotahistAsync(IEnumerable<CotahistPriceRecord> records, CancellationToken ct)
    {
        var list = records.Select(r => new Cotacoes
        {
            DataPregao = r.TradeDate.ToDateTime(TimeOnly.MinValue).Date,
            Ticker = r.Symbol.Trim().ToUpperInvariant(),
            PrecoAbertura = r.Open,
            PrecoFechamento = r.Close,
            PrecoMaximo = r.High,
            PrecoMinimo = r.Low,
        }).ToList();

        await _repo.UpsertBatchAsync(list, ct);
        return list.Count;
    }

    public async Task<int> SaveAsync(IEnumerable<CotacaoIngestDto> dtos, CancellationToken ct)
    {
        var list = dtos.Select(d => new Cotacoes
        {
            DataPregao = d.DataPregao.Date,
            Ticker = d.Ticker.Trim().ToUpperInvariant(),
            PrecoAbertura = d.PrecoAbertura,
            PrecoFechamento = d.PrecoFechamento,
            PrecoMaximo = d.PrecoMaximo,
            PrecoMinimo = d.PrecoMinimo,
        }).ToList();

        await _repo.UpsertBatchAsync(list, ct);
        return list.Count;
    }

    public Task<CotacaoIngestDto?> GetByIdAsync(int id, CancellationToken ct)
        => _repo.GetByIdAsync(id, ct);

    public Task<PagedResponse<CotacaoIngestDto>> ListAsync(PagedRequest request, string? ticker, DateTime? dataPregao, CancellationToken ct)
        => _repo.ListAsync(request, ticker, dataPregao, ct);
}

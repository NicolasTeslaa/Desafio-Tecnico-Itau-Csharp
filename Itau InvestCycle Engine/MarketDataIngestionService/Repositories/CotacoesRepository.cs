using ClassLibrary.Contracts.DTOs;
using ClassLibrary.Domain.Entities;
using Itau.InvestCycleEngine.Contracts.Common;
using MarketDataIngestionService.Data;
using MarketDataIngestionService.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MarketDataIngestionService.Repositories;

public sealed class CotacoesRepository : ICotacoesRepository
{
    private readonly MarketDataDbContext _db;

    public CotacoesRepository(MarketDataDbContext db)
    {
        _db = db;
    }

    public async Task UpsertBatchAsync(IEnumerable<Cotacoes> items, CancellationToken ct)
    {
        var list = items.ToList();
        if (list.Count == 0) return;

        foreach (var c in list)
            c.Ticker = (c.Ticker ?? "").Trim().ToUpperInvariant();

        var tickers = list.Select(x => x.Ticker).Distinct().ToList();
        var dates = list.Select(x => x.DataPregao.Date).Distinct().ToList();

        var existing = await _db.Cotacoes
            .Where(x => tickers.Contains(x.Ticker) && dates.Contains(x.DataPregao.Date))
            .ToListAsync(ct);

        var map = existing.ToDictionary(
            x => (Date: x.DataPregao.Date, Ticker: x.Ticker),
            x => x
        );

        foreach (var incoming in list)
        {
            var key = (incoming.DataPregao.Date, incoming.Ticker);

            if (map.TryGetValue(key, out var current))
            {
                current.PrecoAbertura = incoming.PrecoAbertura;
                current.PrecoFechamento = incoming.PrecoFechamento;
                current.PrecoMaximo = incoming.PrecoMaximo;
                current.PrecoMinimo = incoming.PrecoMinimo;
            }
            else
            {
                incoming.DataPregao = incoming.DataPregao.Date;
                _db.Cotacoes.Add(incoming);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<CotacaoIngestDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await _db.Cotacoes
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new CotacaoIngestDto(
                x.DataPregao,
                x.Ticker,
                x.PrecoAbertura,
                x.PrecoFechamento,
                x.PrecoMaximo,
                x.PrecoMinimo))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PagedResponse<CotacaoIngestDto>> ListAsync(PagedRequest request, string? ticker, DateTime? dataPregao, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : request.PageSize;

        var query = _db.Cotacoes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(ticker))
        {
            var tickerNorm = ticker.Trim().ToUpperInvariant();
            query = query.Where(x => x.Ticker == tickerNorm);
        }

        if (dataPregao.HasValue)
        {
            var date = dataPregao.Value.Date;
            query = query.Where(x => x.DataPregao == date);
        }

        var totalItems = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.DataPregao)
            .ThenBy(x => x.Ticker)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CotacaoIngestDto(
                x.DataPregao,
                x.Ticker,
                x.PrecoAbertura,
                x.PrecoFechamento,
                x.PrecoMaximo,
                x.PrecoMinimo))
            .ToListAsync(ct);

        return new PagedResponse<CotacaoIngestDto>(items, page, pageSize, totalItems);
    }
}

using ClassLibrary.Contracts.DTOs;
using ClassLibrary.Domain.Entities;
using Itau.InvestCycleEngine.Contracts.Common;
using MarketDataIngestionService.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MarketDataIngestionService.Services;

public sealed class CotacoesService : ICotacoesService
{
    private const int BatchSize = 5000;

    private readonly IUnitOfWork _uow;
    private readonly ILogger<CotacoesService> _logger;

    public CotacoesService(IUnitOfWork uow, ILogger<CotacoesService> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<int> SaveFromCotahistAsync(IEnumerable<CotahistPriceRecord> records, CancellationToken ct)
    {
        var totalSaved = 0;
        var batch = new List<Cotacoes>(BatchSize);

        foreach (var r in records)
        {
            batch.Add(new Cotacoes
            {
                DataPregao = r.TradeDate.ToDateTime(TimeOnly.MinValue).Date,
                Ticker = r.Symbol.Trim().ToUpperInvariant(),
                PrecoAbertura = r.Open,
                PrecoFechamento = r.Close,
                PrecoMaximo = r.High,
                PrecoMinimo = r.Low,
            });

            if (batch.Count < BatchSize) continue;

            await UpsertBatchAsync(batch, ct);
            totalSaved += batch.Count;
            batch.Clear();
        }

        if (batch.Count > 0)
        {
            await UpsertBatchAsync(batch, ct);
            totalSaved += batch.Count;
        }

        return totalSaved;
    }

    public async Task<int> SaveAsync(IEnumerable<CotacaoIngestDto> dtos, CancellationToken ct)
    {
        var totalSaved = 0;
        var batch = new List<Cotacoes>(BatchSize);

        foreach (var d in dtos)
        {
            batch.Add(new Cotacoes
            {
                DataPregao = d.DataPregao.Date,
                Ticker = d.Ticker.Trim().ToUpperInvariant(),
                PrecoAbertura = d.PrecoAbertura,
                PrecoFechamento = d.PrecoFechamento,
                PrecoMaximo = d.PrecoMaximo,
                PrecoMinimo = d.PrecoMinimo,
            });

            if (batch.Count < BatchSize) continue;

            await UpsertBatchAsync(batch, ct);
            totalSaved += batch.Count;
            batch.Clear();
        }

        if (batch.Count > 0)
        {
            await UpsertBatchAsync(batch, ct);
            totalSaved += batch.Count;
        }

        return totalSaved;
    }

    public async Task<CotacaoIngestDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await _uow.Repository<Cotacoes>()
            .Query()
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new CotacaoIngestDto(
                x.Id,
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

        var query = _uow.Repository<Cotacoes>().Query().AsNoTracking();

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
                x.Id,
                x.DataPregao,
                x.Ticker,
                x.PrecoAbertura,
                x.PrecoFechamento,
                x.PrecoMaximo,
                x.PrecoMinimo))
            .ToListAsync(ct);

        return new PagedResponse<CotacaoIngestDto>(items, page, pageSize, totalItems);
    }

    public async Task<IReadOnlyList<string>> ListDistinctTickersAsync(string? query, int limit, CancellationToken ct)
    {
        var safeLimit = Math.Clamp(limit, 1, 2000);
        var normalizedQuery = (query ?? string.Empty).Trim().ToUpperInvariant();

        var tickersQuery = _uow.Repository<Cotacoes>()
            .Query()
            .AsNoTracking()
            .Select(x => x.Ticker.Trim().ToUpper())
            .Where(x => x != string.Empty)
            .Distinct();

        if (!string.IsNullOrEmpty(normalizedQuery))
        {
            tickersQuery = tickersQuery.Where(x => x.Contains(normalizedQuery));
        }

        return await tickersQuery
            .OrderBy(x => x)
            .Take(safeLimit)
            .ToListAsync(ct);
    }

    private async Task UpsertBatchAsync(IEnumerable<Cotacoes> items, CancellationToken ct)
    {
        var list = items
            .Select(x =>
            {
                x.Ticker = (x.Ticker ?? "").Trim().ToUpperInvariant();
                x.DataPregao = x.DataPregao.Date;
                return x;
            })
            .GroupBy(x => BuildQuoteKey(x.DataPregao, x.Ticker))
            .Select(g => g.Last())
            .ToList();
        if (list.Count == 0) return;

        var repo = _uow.Repository<Cotacoes>();

        var tickers = list.Select(x => x.Ticker).Distinct().ToList();
        var incomingKeys = list
            .Select(x => BuildQuoteKey(x.DataPregao, x.Ticker))
            .ToHashSet();

        try
        {
            await _uow.BeginAsync(ct);

            var existing = await repo.Query()
                .Where(x => tickers.Contains(x.Ticker))
                .ToListAsync(ct);
            foreach (var current in existing.Where(x => incomingKeys.Contains(BuildQuoteKey(x.DataPregao.Date, x.Ticker))))
            {
                repo.Remove(current);
            }

            foreach (var incoming in list)
            {
                await repo.AddAsync(incoming, ct);
            }

            await _uow.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no upsert em lote de cotacoes.");
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    private static string BuildQuoteKey(DateTime dataPregao, string ticker)
        => $"{dataPregao:yyyy-MM-dd}|{ticker.Trim().ToUpperInvariant()}";
}

using ClassLibrary.Contracts.DTOs;
using ClassLibrary.Domain.Entities;
using Itau.InvestCycleEngine.Contracts.Common;
using MarketDataIngestionService.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MarketDataIngestionService.Services;

public sealed class CotacoesService : ICotacoesService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<CotacoesService> _logger;

    public CotacoesService(IUnitOfWork uow, ILogger<CotacoesService> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<int> SaveFromCotahistAsync(IEnumerable<CotahistPriceRecord> records, CancellationToken ct)
    {
        try
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

            await UpsertBatchAsync(list, ct);
            return list.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar cotacoes a partir de registros cotahist.");
            throw;
        }
    }

    public async Task<CotacaoIngestDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        try
        {
            return await _uow.Repository<Cotacoes>()
           .Query()
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter cotacao por ID.");
            throw;
        }
    }

    public async Task<PagedResponse<CotacaoIngestDto>> ListAsync(PagedRequest request, string? ticker, DateTime? dataPregao, CancellationToken ct)
    {
        try
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
                    x.DataPregao,
                    x.Ticker,
                    x.PrecoAbertura,
                    x.PrecoFechamento,
                    x.PrecoMaximo,
                    x.PrecoMinimo))
                .ToListAsync(ct);

            return new PagedResponse<CotacaoIngestDto>(items, page, pageSize, totalItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar cotacoes com paginação.");
            throw;
        }
    }

    private async Task UpsertBatchAsync(IEnumerable<Cotacoes> items, CancellationToken ct)
    {
        var list = items.ToList();
        if (list.Count == 0) return;

        foreach (var c in list)
            c.Ticker = (c.Ticker ?? "").Trim().ToUpperInvariant();

        var repo = _uow.Repository<Cotacoes>();

        var tickers = list.Select(x => x.Ticker).Distinct().ToList();
        var dates = list.Select(x => x.DataPregao.Date).Distinct().ToList();

        try
        {
            await _uow.BeginAsync(ct);

            var existing = await repo.Query()
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
                    repo.Update(current);
                }
                else
                {
                    incoming.DataPregao = incoming.DataPregao.Date;
                    await repo.AddAsync(incoming, ct);
                }
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
}
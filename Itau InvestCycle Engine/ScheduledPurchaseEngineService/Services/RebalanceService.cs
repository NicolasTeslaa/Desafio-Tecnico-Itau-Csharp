using ClassLibrary.Domain.Entities;
using ClassLibrary.Domain.Entities.Cestas;
using ClassLibrary.Domain.Entities.Clientes;
using ClassLibrary.Domain.Entities.CompraDistribuicao;
using ClassLibrary.Domain.Entities.RebalanceamentoIR;
using Itau.InvestCycleEngine.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using ScheduledPurchaseEngineService.Interfaces;

namespace ScheduledPurchaseEngineService.Services;

public sealed class RebalanceService : IRebalanceService
{
    private readonly IUnitOfWork _uow;
    private readonly IFinanceEventsPublisher _publisher;
    private readonly ILogger<RebalanceService> _logger;

    public RebalanceService(IUnitOfWork uow, IFinanceEventsPublisher publisher, ILogger<RebalanceService> logger)
    {
        _uow = uow;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<int> RebalanceByBasketChangeAsync(int previousCestaId, int newCestaId, CancellationToken ct = default)
    {
        var itensRepo = _uow.Repository<ItensCesta>();
        var clientesRepo = _uow.Repository<Clientes>();
        var contasRepo = _uow.Repository<ContasGraficas>();
        var custodiasRepo = _uow.Repository<Custodias>();
        var cotacoesRepo = _uow.Repository<Cotacoes>();
        var rebalsRepo = _uow.Repository<Rebalanceamentos>();
        var eventosRepo = _uow.Repository<EventosIR>();

        var previousItems = await itensRepo.Query().AsNoTracking()
            .Where(x => x.CestaId == previousCestaId)
            .ToListAsync(ct);

        var newItems = await itensRepo.Query().AsNoTracking()
            .Where(x => x.CestaId == newCestaId)
            .ToListAsync(ct);

        var oldTickers = previousItems.Select(x => NormalizeTicker(x.Ticker)).ToHashSet();
        var newTickers = newItems.Select(x => NormalizeTicker(x.Ticker)).ToHashSet();
        var tickersRemovidos = oldTickers.Except(newTickers).ToHashSet();
        var tickersAdicionados = newItems
            .Where(x => !oldTickers.Contains(NormalizeTicker(x.Ticker)))
            .Select(x => (Ticker: NormalizeTicker(x.Ticker), x.Percentual))
            .ToList();

        if (tickersRemovidos.Count == 0 && tickersAdicionados.Count == 0)
        {
            return 0;
        }

        var clientesAtivos = await clientesRepo.Query()
            .Where(x => x.Ativo)
            .ToListAsync(ct);

        var clienteIds = clientesAtivos.Select(x => x.Id).ToList();
        var contasFilhote = await contasRepo.Query()
            .Where(x => clienteIds.Contains(x.ClienteId) && x.Tipo == TipoConta.Filhote)
            .ToDictionaryAsync(x => x.ClienteId, ct);

        var eventosParaPublicar = new List<PendingIrEvent>();
        var totalClientesProcessados = 0;
        var now = DateTime.UtcNow;

        await _uow.BeginAsync(ct);
        try
        {
            foreach (var cliente in clientesAtivos)
            {
                if (!contasFilhote.TryGetValue(cliente.Id, out var conta))
                {
                    continue;
                }

                var custodias = await custodiasRepo.Query()
                    .Where(x => x.ContasGraficasId == conta.Id && x.Quantidade > 0)
                    .ToListAsync(ct);

                var custodiasByTicker = custodias.ToDictionary(x => NormalizeTicker(x.Ticker));
                var tickersCotacao = custodiasByTicker.Keys
                    .Concat(tickersAdicionados.Select(x => x.Ticker))
                    .Distinct()
                    .ToList();

                var quotes = await cotacoesRepo.Query()
                    .Where(x => tickersCotacao.Contains(x.Ticker))
                    .ToListAsync(ct);

                var quoteByTicker = quotes
                    .GroupBy(x => NormalizeTicker(x.Ticker))
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.DataPregao).First().PrecoFechamento);

                decimal totalVendas = 0m;
                decimal lucroLiquido = 0m;

                foreach (var tickerVendido in tickersRemovidos)
                {
                    if (!custodiasByTicker.TryGetValue(tickerVendido, out var custodia) || custodia.Quantidade <= 0)
                    {
                        continue;
                    }

                    if (!quoteByTicker.TryGetValue(tickerVendido, out var quoteVenda) || quoteVenda <= 0m)
                    {
                        continue;
                    }

                    var valorVenda = Math.Round(custodia.Quantidade * quoteVenda, 2);
                    totalVendas += valorVenda;
                    lucroLiquido += Math.Round((quoteVenda - custodia.PrecoMedio) * custodia.Quantidade, 2);

                    await rebalsRepo.AddAsync(new Rebalanceamentos
                    {
                        ClienteId = cliente.Id,
                        TickerVendido = tickerVendido,
                        TickerComprado = "CAIXA",
                        ValorVenda = valorVenda,
                        DataRebalanceamento = now
                    }, ct);

                    custodiasRepo.Remove(custodia);
                }

                if (totalVendas > 0m && tickersAdicionados.Count > 0)
                {
                    var totalPercent = tickersAdicionados.Sum(x => x.Percentual);
                    if (totalPercent > 0m)
                    {
                        foreach (var add in tickersAdicionados)
                        {
                            if (!quoteByTicker.TryGetValue(add.Ticker, out var quoteCompra) || quoteCompra <= 0m)
                            {
                                continue;
                            }

                            var valorAlocado = Math.Round(totalVendas * (add.Percentual / totalPercent), 2);
                            var qtyCompra = (int)Math.Floor(valorAlocado / quoteCompra);
                            if (qtyCompra <= 0)
                            {
                                continue;
                            }

                            if (custodiasByTicker.TryGetValue(add.Ticker, out var custExistente))
                            {
                                var qtdAnterior = custExistente.Quantidade;
                                var qtdNova = qtdAnterior + qtyCompra;
                                custExistente.PrecoMedio = Math.Round(
                                    ((qtdAnterior * custExistente.PrecoMedio) + (qtyCompra * quoteCompra)) / qtdNova, 6);
                                custExistente.Quantidade = qtdNova;
                                custExistente.DataUltimaAtualizacao = now;
                                custodiasRepo.Update(custExistente);
                            }
                            else
                            {
                                var nova = new Custodias
                                {
                                    ContasGraficasId = conta.Id,
                                    Ticker = add.Ticker,
                                    Quantidade = qtyCompra,
                                    PrecoMedio = quoteCompra,
                                    DataUltimaAtualizacao = now
                                };
                                await custodiasRepo.AddAsync(nova, ct);
                                custodiasByTicker[add.Ticker] = nova;
                            }
                        }
                    }
                }

                if (totalVendas > 0m)
                {
                    var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    var monthEnd = monthStart.AddMonths(1);

                    var totalVendasMesHistorico = await rebalsRepo.Query()
                        .Where(x => x.ClienteId == cliente.Id &&
                                    x.DataRebalanceamento >= monthStart &&
                                    x.DataRebalanceamento < monthEnd)
                        .SumAsync(x => (decimal?)x.ValorVenda, ct) ?? 0m;

                    var totalVendasMes = totalVendasMesHistorico + totalVendas;

                    if (totalVendasMes > 20000m && lucroLiquido > 0m)
                    {
                        var evento = new EventosIR
                        {
                            ClienteId = cliente.Id,
                            Tipo = TipoIR.IR_Venda,
                            ValorBase = Math.Round(lucroLiquido, 2),
                            ValorIR = Math.Round(lucroLiquido * 0.20m, 2),
                            PublicadoKafka = false,
                            DataEvento = now
                        };

                        await eventosRepo.AddAsync(evento, ct);
                        eventosParaPublicar.Add(new PendingIrEvent(evento, cliente.CPF, "REBAL"));
                    }

                    totalClientesProcessados++;
                }
            }

            await _uow.CommitAsync(ct);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }

        await PublishAndMarkAsync(eventosParaPublicar, ct);
        return totalClientesProcessados;
    }

    public async Task<(int Evaluated, int Rebalanced)> RebalanceByDriftAsync(decimal thresholdPercentual, CancellationToken ct = default)
    {
        var itensRepo = _uow.Repository<ItensCesta>();
        var clientesRepo = _uow.Repository<Clientes>();
        var contasRepo = _uow.Repository<ContasGraficas>();
        var custodiasRepo = _uow.Repository<Custodias>();
        var cotacoesRepo = _uow.Repository<Cotacoes>();
        var rebalsRepo = _uow.Repository<Rebalanceamentos>();
        var eventosRepo = _uow.Repository<EventosIR>();

        var cestaAtiva = await _uow.Repository<CestasRecomendacao>().Query()
            .AsNoTracking()
            .Where(x => x.Ativa)
            .OrderByDescending(x => x.DataCriacao)
            .FirstOrDefaultAsync(ct);

        if (cestaAtiva is null)
        {
            return (0, 0);
        }

        var itensCesta = await itensRepo.Query().AsNoTracking()
            .Where(x => x.CestaId == cestaAtiva.Id)
            .ToListAsync(ct);

        var targetPct = itensCesta.ToDictionary(x => NormalizeTicker(x.Ticker), x => x.Percentual);
        var basketTickers = targetPct.Keys.ToList();

        var clientesAtivos = await clientesRepo.Query().Where(x => x.Ativo).ToListAsync(ct);
        var clienteIds = clientesAtivos.Select(x => x.Id).ToList();

        var contasFilhote = await contasRepo.Query()
            .Where(x => clienteIds.Contains(x.ClienteId) && x.Tipo == TipoConta.Filhote)
            .ToDictionaryAsync(x => x.ClienteId, ct);

        var eventosParaPublicar = new List<PendingIrEvent>();
        var evaluated = 0;
        var rebalanced = 0;
        var now = DateTime.UtcNow;

        await _uow.BeginAsync(ct);
        try
        {
            foreach (var cliente in clientesAtivos)
            {
                if (!contasFilhote.TryGetValue(cliente.Id, out var conta))
                {
                    continue;
                }

                evaluated++;

                var custodias = await custodiasRepo.Query()
                    .Where(x => x.ContasGraficasId == conta.Id && x.Quantidade > 0)
                    .ToListAsync(ct);

                if (custodias.Count == 0)
                {
                    continue;
                }

                var tickers = custodias.Select(x => NormalizeTicker(x.Ticker))
                    .Concat(basketTickers)
                    .Distinct()
                    .ToList();

                var quotes = await cotacoesRepo.Query()
                    .Where(x => tickers.Contains(x.Ticker))
                    .ToListAsync(ct);

                var quoteByTicker = quotes
                    .GroupBy(x => NormalizeTicker(x.Ticker))
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.DataPregao).First().PrecoFechamento);

                var holdings = custodias
                    .ToDictionary(x => NormalizeTicker(x.Ticker), x => x);

                decimal totalCarteira = 0m;
                foreach (var h in holdings)
                {
                    if (!quoteByTicker.TryGetValue(h.Key, out var q) || q <= 0m) continue;
                    totalCarteira += h.Value.Quantidade * q;
                }

                if (totalCarteira <= 0m)
                {
                    continue;
                }

                var driftDetected = false;
                var diffByTicker = new Dictionary<string, decimal>();
                foreach (var ticker in basketTickers)
                {
                    var target = targetPct[ticker];
                    var currentValue = 0m;
                    if (holdings.TryGetValue(ticker, out var h) && quoteByTicker.TryGetValue(ticker, out var q) && q > 0m)
                    {
                        currentValue = h.Quantidade * q;
                    }

                    var currentPct = totalCarteira > 0m ? (currentValue / totalCarteira) * 100m : 0m;
                    if (Math.Abs(currentPct - target) > thresholdPercentual)
                    {
                        driftDetected = true;
                    }

                    var targetValue = totalCarteira * (target / 100m);
                    diffByTicker[ticker] = targetValue - currentValue;
                }

                if (!driftDetected)
                {
                    continue;
                }

                decimal totalVendas = 0m;
                decimal lucroLiquido = 0m;

                foreach (var kv in diffByTicker.Where(x => x.Value < 0m).ToList())
                {
                    var ticker = kv.Key;
                    if (!holdings.TryGetValue(ticker, out var custodia)) continue;
                    if (!quoteByTicker.TryGetValue(ticker, out var quoteVenda) || quoteVenda <= 0m) continue;

                    var valorVendaDesejado = Math.Abs(kv.Value);
                    var qtySell = (int)Math.Floor(valorVendaDesejado / quoteVenda);
                    qtySell = Math.Min(qtySell, custodia.Quantidade);
                    if (qtySell <= 0) continue;

                    var valorVenda = Math.Round(qtySell * quoteVenda, 2);
                    totalVendas += valorVenda;
                    lucroLiquido += Math.Round((quoteVenda - custodia.PrecoMedio) * qtySell, 2);

                    custodia.Quantidade -= qtySell;
                    custodia.DataUltimaAtualizacao = now;
                    if (custodia.Quantidade <= 0)
                    {
                        custodiasRepo.Remove(custodia);
                        holdings.Remove(ticker);
                    }
                    else
                    {
                        custodiasRepo.Update(custodia);
                    }

                    await rebalsRepo.AddAsync(new Rebalanceamentos
                    {
                        ClienteId = cliente.Id,
                        TickerVendido = ticker,
                        TickerComprado = "CAIXA",
                        ValorVenda = valorVenda,
                        DataRebalanceamento = now
                    }, ct);
                }

                if (totalVendas > 0m)
                {
                    var totalDeficit = diffByTicker.Where(x => x.Value > 0m).Sum(x => x.Value);
                    if (totalDeficit > 0m)
                    {
                        foreach (var kv in diffByTicker.Where(x => x.Value > 0m).ToList())
                        {
                            var ticker = kv.Key;
                            if (!quoteByTicker.TryGetValue(ticker, out var quoteCompra) || quoteCompra <= 0m) continue;

                            var valorAlocado = Math.Round(totalVendas * (kv.Value / totalDeficit), 2);
                            var qtyBuy = (int)Math.Floor(valorAlocado / quoteCompra);
                            if (qtyBuy <= 0) continue;

                            if (holdings.TryGetValue(ticker, out var custodia))
                            {
                                var qtdAnterior = custodia.Quantidade;
                                var qtdNova = qtdAnterior + qtyBuy;
                                custodia.PrecoMedio = Math.Round(
                                    ((qtdAnterior * custodia.PrecoMedio) + (qtyBuy * quoteCompra)) / qtdNova, 6);
                                custodia.Quantidade = qtdNova;
                                custodia.DataUltimaAtualizacao = now;
                                custodiasRepo.Update(custodia);
                            }
                            else
                            {
                                var nova = new Custodias
                                {
                                    ContasGraficasId = conta.Id,
                                    Ticker = ticker,
                                    Quantidade = qtyBuy,
                                    PrecoMedio = quoteCompra,
                                    DataUltimaAtualizacao = now
                                };
                                await custodiasRepo.AddAsync(nova, ct);
                                holdings[ticker] = nova;
                            }
                        }
                    }

                    var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    var monthEnd = monthStart.AddMonths(1);
                    var totalVendasMesHistorico = await rebalsRepo.Query()
                        .Where(x => x.ClienteId == cliente.Id &&
                                    x.DataRebalanceamento >= monthStart &&
                                    x.DataRebalanceamento < monthEnd)
                        .SumAsync(x => (decimal?)x.ValorVenda, ct) ?? 0m;
                    var totalVendasMes = totalVendasMesHistorico + totalVendas;

                    if (totalVendasMes > 20000m && lucroLiquido > 0m)
                    {
                        var evento = new EventosIR
                        {
                            ClienteId = cliente.Id,
                            Tipo = TipoIR.IR_Venda,
                            ValorBase = Math.Round(lucroLiquido, 2),
                            ValorIR = Math.Round(lucroLiquido * 0.20m, 2),
                            PublicadoKafka = false,
                            DataEvento = now
                        };
                        await eventosRepo.AddAsync(evento, ct);
                        eventosParaPublicar.Add(new PendingIrEvent(evento, cliente.CPF, "REBAL"));
                    }

                    rebalanced++;
                }
            }

            await _uow.CommitAsync(ct);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }

        await PublishAndMarkAsync(eventosParaPublicar, ct);
        return (evaluated, rebalanced);
    }

    private async Task PublishAndMarkAsync(IReadOnlyList<PendingIrEvent> events, CancellationToken ct)
    {
        if (events.Count == 0) return;

        var eventosRepo = _uow.Repository<EventosIR>();

        foreach (var pending in events)
        {
            var evt = pending.Event;
            try
            {
                if (evt.Tipo == TipoIR.IR_Venda)
                {
                    await _publisher.PublishIrVendaAsync(evt, pending.Cpf, pending.Ticker, ct);
                }
                else
                {
                    await _publisher.PublishIrDedoDuroAsync(evt, pending.Cpf, pending.Ticker, ct);
                }

                await _uow.BeginAsync(ct);
                evt.PublicadoKafka = true;
                eventosRepo.Update(evt);
                await _uow.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao publicar evento IR {EventId} no Kafka.", evt.Id);
                throw new InvalidOperationException("KAFKA_INDISPONIVEL");
            }
        }
    }

    private sealed record PendingIrEvent(EventosIR Event, string Cpf, string Ticker);

    private static string NormalizeTicker(string ticker) => ticker.Trim().ToUpperInvariant();
}

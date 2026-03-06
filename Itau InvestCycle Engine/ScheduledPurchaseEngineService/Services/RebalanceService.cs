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
        var precosMediosRepo = _uow.Repository<PrecoMedio>();
        var rebalsRepo = _uow.Repository<Rebalanceamentos>();
        var eventosRepo = _uow.Repository<EventosIR>();

        var previousItems = await itensRepo.Query().AsNoTracking()
            .Where(x => x.CestaId == previousCestaId)
            .ToListAsync(ct);

        var newItems = await itensRepo.Query().AsNoTracking()
            .Where(x => x.CestaId == newCestaId)
            .ToListAsync(ct);

        var oldPercentByTicker = previousItems
            .GroupBy(x => NormalizeTicker(x.Ticker))
            .ToDictionary(g => g.Key, g => g.First().Percentual);

        var newPercentByTicker = newItems
            .GroupBy(x => NormalizeTicker(x.Ticker))
            .ToDictionary(g => g.Key, g => g.First().Percentual);

        var oldTickers = oldPercentByTicker.Keys.ToHashSet();
        var newTickers = newPercentByTicker.Keys.ToHashSet();
        var tickersRemovidos = oldTickers.Except(newTickers).ToHashSet();
        var tickersAdicionados = newTickers.Except(oldTickers).ToHashSet();
        var tickersMantidosComMudancaPercentual = oldTickers
            .Intersect(newTickers)
            .Where(t => Math.Abs(oldPercentByTicker[t] - newPercentByTicker[t]) > 0.0001m)
            .ToHashSet();

        if (tickersRemovidos.Count == 0 &&
            tickersAdicionados.Count == 0 &&
            tickersMantidosComMudancaPercentual.Count == 0)
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
                    .Concat(newPercentByTicker.Keys)
                    .Distinct()
                    .ToList();

                var quotes = await cotacoesRepo.Query()
                    .Where(x => tickersCotacao.Contains(x.Ticker))
                    .ToListAsync(ct);

                var quoteByTicker = quotes
                    .GroupBy(x => NormalizeTicker(x.Ticker))
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.DataPregao).First().PrecoFechamento);

                var houveMovimentacao = false;
                decimal caixaDisponivel = 0m;
                decimal totalVendas = 0m;
                decimal lucroLiquido = 0m;
                var detalhesIrVenda = new List<IrVendaKafkaDetail>();

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
                    var lucroVenda = Math.Round((quoteVenda - custodia.PrecoMedio) * custodia.Quantidade, 2);
                    caixaDisponivel += valorVenda;
                    totalVendas += valorVenda;
                    lucroLiquido += lucroVenda;
                    detalhesIrVenda.Add(new IrVendaKafkaDetail(
                        Ticker: tickerVendido,
                        Quantidade: custodia.Quantidade,
                        PrecoVenda: quoteVenda,
                        PrecoMedio: custodia.PrecoMedio,
                        Lucro: lucroVenda));

                    await rebalsRepo.AddAsync(CreateSaleRebalanceamento(
                        cliente.Id,
                        tickerVendido,
                        custodia.Quantidade,
                        quoteVenda,
                        valorVenda,
                        now), ct);

                    custodia.Quantidade = 0;
                    custodia.DataUltimaAtualizacao = now;
                    custodiasRepo.Update(custodia);
                    await PersistedStructureSync.UpsertPrecoMedioAsync(
                        precosMediosRepo,
                        custodia,
                        custodia.PrecoMedio,
                        custodia.DataUltimaAtualizacao,
                        ct);
                    houveMovimentacao = true;
                }

                decimal valorCarteiraPosRemocao = 0m;
                foreach (var ticker in newPercentByTicker.Keys)
                {
                    if (!custodiasByTicker.TryGetValue(ticker, out var custodiaAtual) || custodiaAtual.Quantidade <= 0)
                    {
                        continue;
                    }

                    if (!quoteByTicker.TryGetValue(ticker, out var quoteAtual) || quoteAtual <= 0m)
                    {
                        continue;
                    }

                    valorCarteiraPosRemocao += custodiaAtual.Quantidade * quoteAtual;
                }

                var valorBaseRebalance = valorCarteiraPosRemocao + caixaDisponivel;
                if (valorBaseRebalance > 0m)
                {
                    // Primeiro ajusta excesso (vendas), depois usa o caixa resultante para compras.
                    foreach (var ticker in newPercentByTicker.Keys)
                    {
                        if (!custodiasByTicker.TryGetValue(ticker, out var custodiaAtual) || custodiaAtual.Quantidade <= 0)
                        {
                            continue;
                        }

                        if (!quoteByTicker.TryGetValue(ticker, out var quoteVenda) || quoteVenda <= 0m)
                        {
                            continue;
                        }

                        var valorAtual = custodiaAtual.Quantidade * quoteVenda;
                        var valorAlvo = valorBaseRebalance * (newPercentByTicker[ticker] / 100m);
                        var excesso = valorAtual - valorAlvo;
                        if (excesso <= 0m)
                        {
                            continue;
                        }

                        var qtySell = (int)Math.Floor(excesso / quoteVenda);
                        qtySell = Math.Min(qtySell, custodiaAtual.Quantidade);
                        if (qtySell <= 0)
                        {
                            continue;
                        }

                        var valorVenda = Math.Round(qtySell * quoteVenda, 2);
                        var lucroVenda = Math.Round((quoteVenda - custodiaAtual.PrecoMedio) * qtySell, 2);
                        caixaDisponivel += valorVenda;
                        totalVendas += valorVenda;
                        lucroLiquido += lucroVenda;
                        detalhesIrVenda.Add(new IrVendaKafkaDetail(
                            Ticker: ticker,
                            Quantidade: qtySell,
                            PrecoVenda: quoteVenda,
                            PrecoMedio: custodiaAtual.PrecoMedio,
                            Lucro: lucroVenda));

                        custodiaAtual.Quantidade -= qtySell;
                        custodiaAtual.DataUltimaAtualizacao = now;

                        if (custodiaAtual.Quantidade <= 0)
                        {
                            custodiaAtual.Quantidade = 0;
                            custodiasRepo.Update(custodiaAtual);
                            await PersistedStructureSync.UpsertPrecoMedioAsync(
                                precosMediosRepo,
                                custodiaAtual,
                                custodiaAtual.PrecoMedio,
                                custodiaAtual.DataUltimaAtualizacao,
                                ct);
                        }
                        else
                        {
                            custodiasRepo.Update(custodiaAtual);
                        }

                        await rebalsRepo.AddAsync(CreateSaleRebalanceamento(
                            cliente.Id,
                            ticker,
                            qtySell,
                            quoteVenda,
                            valorVenda,
                            now), ct);

                        houveMovimentacao = true;
                    }

                    var deficits = new List<(string Ticker, decimal Deficit, decimal Quote)>();
                    var totalDeficit = 0m;

                    foreach (var ticker in newPercentByTicker.Keys)
                    {
                        if (!quoteByTicker.TryGetValue(ticker, out var quoteCompra) || quoteCompra <= 0m)
                        {
                            continue;
                        }

                        var valorAtual = 0m;
                        if (custodiasByTicker.TryGetValue(ticker, out var custodiaAtual) && custodiaAtual.Quantidade > 0)
                        {
                            valorAtual = custodiaAtual.Quantidade * quoteCompra;
                        }

                        var valorAlvo = valorBaseRebalance * (newPercentByTicker[ticker] / 100m);
                        var deficit = valorAlvo - valorAtual;
                        if (deficit <= 0m)
                        {
                            continue;
                        }

                        deficits.Add((ticker, deficit, quoteCompra));
                        totalDeficit += deficit;
                    }

                    if (caixaDisponivel > 0m && totalDeficit > 0m)
                    {
                        var caixaParaCompras = caixaDisponivel;

                        foreach (var item in deficits)
                        {
                            var valorAlocado = Math.Round(caixaParaCompras * (item.Deficit / totalDeficit), 2);
                            var qtyCompra = (int)Math.Floor(valorAlocado / item.Quote);
                            if (qtyCompra <= 0)
                            {
                                continue;
                            }

                            var valorCompra = Math.Round(qtyCompra * item.Quote, 2);
                            caixaDisponivel = Math.Max(0m, caixaDisponivel - valorCompra);

                            if (custodiasByTicker.TryGetValue(item.Ticker, out var custExistente))
                            {
                                var qtdAnterior = custExistente.Quantidade;
                                var qtdNova = qtdAnterior + qtyCompra;
                                custExistente.PrecoMedio = Math.Round(
                                    ((qtdAnterior * custExistente.PrecoMedio) + (qtyCompra * item.Quote)) / qtdNova, 6);
                                custExistente.Quantidade = qtdNova;
                                custExistente.DataUltimaAtualizacao = now;
                                custodiasRepo.Update(custExistente);
                                await PersistedStructureSync.UpsertPrecoMedioAsync(precosMediosRepo, custExistente, custExistente.PrecoMedio, custExistente.DataUltimaAtualizacao, ct);
                            }
                            else
                            {
                                var nova = new Custodias
                                {
                                    ContasGraficasId = conta.Id,
                                    Ticker = item.Ticker,
                                    Quantidade = qtyCompra,
                                    PrecoMedio = item.Quote,
                                    DataUltimaAtualizacao = now
                                };
                                await custodiasRepo.AddAsync(nova, ct);
                                await PersistedStructureSync.UpsertPrecoMedioAsync(precosMediosRepo, nova, nova.PrecoMedio, nova.DataUltimaAtualizacao, ct);
                                custodiasByTicker[item.Ticker] = nova;
                            }

                            await rebalsRepo.AddAsync(CreateBuyRebalanceamento(
                                cliente.Id,
                                item.Ticker,
                                qtyCompra,
                                item.Quote,
                                now), ct);

                            houveMovimentacao = true;
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
                        eventosParaPublicar.Add(new PendingIrEvent(
                            evento,
                            cliente.CPF,
                            new IrVendaKafkaPayload(
                                MesReferencia: $"{now:yyyy-MM}",
                                TotalVendasMes: Math.Round(totalVendasMes, 2),
                                LucroLiquido: Math.Round(lucroLiquido, 2),
                                Aliquota: 0.20m,
                                Detalhes: detalhesIrVenda,
                                DataCalculo: now)));
                    }
                }

                if (houveMovimentacao)
                {
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
        var precosMediosRepo = _uow.Repository<PrecoMedio>();
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
                var detalhesIrVenda = new List<IrVendaKafkaDetail>();

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
                    var lucroVenda = Math.Round((quoteVenda - custodia.PrecoMedio) * qtySell, 2);
                    totalVendas += valorVenda;
                    lucroLiquido += lucroVenda;
                    detalhesIrVenda.Add(new IrVendaKafkaDetail(
                        Ticker: ticker,
                        Quantidade: qtySell,
                        PrecoVenda: quoteVenda,
                        PrecoMedio: custodia.PrecoMedio,
                        Lucro: lucroVenda));

                    custodia.Quantidade -= qtySell;
                    custodia.DataUltimaAtualizacao = now;
                    if (custodia.Quantidade <= 0)
                    {
                        custodia.Quantidade = 0;
                        custodiasRepo.Update(custodia);
                        await PersistedStructureSync.UpsertPrecoMedioAsync(
                            precosMediosRepo,
                            custodia,
                            custodia.PrecoMedio,
                            custodia.DataUltimaAtualizacao,
                            ct);
                    }
                    else
                    {
                        custodiasRepo.Update(custodia);
                    }

                    await rebalsRepo.AddAsync(CreateSaleRebalanceamento(
                        cliente.Id,
                        ticker,
                        qtySell,
                        quoteVenda,
                        valorVenda,
                        now), ct);
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
                                await PersistedStructureSync.UpsertPrecoMedioAsync(precosMediosRepo, custodia, custodia.PrecoMedio, custodia.DataUltimaAtualizacao, ct);
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
                                await PersistedStructureSync.UpsertPrecoMedioAsync(precosMediosRepo, nova, nova.PrecoMedio, nova.DataUltimaAtualizacao, ct);
                                holdings[ticker] = nova;
                            }

                            await rebalsRepo.AddAsync(CreateBuyRebalanceamento(
                                cliente.Id,
                                ticker,
                                qtyBuy,
                                quoteCompra,
                                now), ct);
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
                        eventosParaPublicar.Add(new PendingIrEvent(
                            evento,
                            cliente.CPF,
                            new IrVendaKafkaPayload(
                                MesReferencia: $"{now:yyyy-MM}",
                                TotalVendasMes: Math.Round(totalVendasMes, 2),
                                LucroLiquido: Math.Round(lucroLiquido, 2),
                                Aliquota: 0.20m,
                                Detalhes: detalhesIrVenda,
                                DataCalculo: now)));
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
                    await _publisher.PublishIrVendaAsync(evt, pending.Cpf, pending.IrVendaPayload!, ct);
                }
                else
                {
                    await _publisher.PublishIrDedoDuroAsync(evt, pending.Cpf, pending.Ticker!, ct);
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

    private sealed record PendingIrEvent(
        EventosIR Event,
        string Cpf,
        IrVendaKafkaPayload? IrVendaPayload = null,
        string? Ticker = null);

    private static Rebalanceamentos CreateSaleRebalanceamento(
        int clienteId,
        string ticker,
        int quantidade,
        decimal precoUnitario,
        decimal valorVenda,
        DateTime dataRebalanceamento)
        => new()
        {
            ClienteId = clienteId,
            TickerVendido = ticker,
            TickerComprado = "CAIXA",
            QuantidadeVendida = quantidade,
            PrecoUnitarioVenda = precoUnitario,
            ValorVenda = valorVenda,
            DataRebalanceamento = dataRebalanceamento
        };

    private static Rebalanceamentos CreateBuyRebalanceamento(
        int clienteId,
        string ticker,
        int quantidade,
        decimal precoUnitario,
        DateTime dataRebalanceamento)
        => new()
        {
            ClienteId = clienteId,
            TickerVendido = "CAIXA",
            TickerComprado = ticker,
            QuantidadeComprada = quantidade,
            PrecoUnitarioCompra = precoUnitario,
            ValorVenda = 0m,
            DataRebalanceamento = dataRebalanceamento
        };

    private static string NormalizeTicker(string ticker) => ticker.Trim().ToUpperInvariant();
}

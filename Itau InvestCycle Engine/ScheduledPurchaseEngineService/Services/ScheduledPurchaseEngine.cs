using ClassLibrary.Contracts.DTOs;
using ClassLibrary.Domain.Entities;
using ClassLibrary.Domain.Entities.Cestas;
using ClassLibrary.Domain.Entities.Clientes;
using ClassLibrary.Domain.Entities.CompraDistribuicao;
using ClassLibrary.Domain.Entities.RebalanceamentoIR;
using Itau.InvestCycleEngine.Domain.Entities;
using Itau.InvestCycleEngine.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using ScheduledPurchaseEngineService.Interfaces;

namespace ScheduledPurchaseEngineService.Services;

public sealed class ScheduledPurchaseEngine : IScheduledPurchaseEngine
{
    private readonly IUnitOfWork _uow;
    private readonly ITradingCalendar _tradingCalendar;
    private readonly IFinanceEventsPublisher _publisher;
    private readonly ILogger<ScheduledPurchaseEngine> _logger;

    public ScheduledPurchaseEngine(
        IUnitOfWork uow,
        ITradingCalendar tradingCalendar,
        IFinanceEventsPublisher publisher,
        ILogger<ScheduledPurchaseEngine> logger)
    {
        _uow = uow;
        _tradingCalendar = tradingCalendar;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<ScheduledPurchaseResult> ExecuteAsync(DateOnly referenceDate, CancellationToken ct = default)
    {
        var clientesRepo = _uow.Repository<Clientes>();
        var contasRepo = _uow.Repository<ContasGraficas>();
        var cestasRepo = _uow.Repository<CestasRecomendacao>();
        var itensCestaRepo = _uow.Repository<ItensCesta>();
        var cotacoesRepo = _uow.Repository<Cotacoes>();
        var custodiasRepo = _uow.Repository<Custodias>();
        var ordensRepo = _uow.Repository<OrdensCompra>();
        var distribuicoesRepo = _uow.Repository<Distribuicoes>();
        var eventosIrRepo = _uow.Repository<EventosIR>();
        var execucoesRepo = _uow.Repository<MotorExecucao>();

        if (!_tradingCalendar.IsPurchaseDate(referenceDate))
        {
            throw new InvalidOperationException("DATA_EXECUCAO_INVALIDA");
        }

        var execucao = await StartExecutionAsync(execucoesRepo, referenceDate, ct);

        var clientesAtivos = await clientesRepo.Query()
            .Where(x => x.Ativo)
            .ToListAsync(ct);

        var aportes = clientesAtivos
            .Select(c => new
            {
                Cliente = c,
                Aporte = Math.Round(c.ValorMensal / 3m, 2)
            })
            .Where(x => x.Aporte > 0m)
            .ToList();

        if (aportes.Count == 0)
        {
            await MarkExecutionSuccessAsync(execucoesRepo, execucao, ct);
            return new ScheduledPurchaseResult(
                DateTimeOffset.UtcNow,
                referenceDate,
                0,
                0m,
                [],
                [],
                [],
                0);
        }

        var cestaAtiva = await cestasRepo.Query()
            .Where(x => x.Ativa)
            .OrderByDescending(x => x.DataCriacao)
            .FirstOrDefaultAsync(ct);

        if (cestaAtiva is null)
        {
            throw new InvalidOperationException("CESTA_NAO_ENCONTRADA");
        }

        var itensCesta = await itensCestaRepo.Query()
            .Where(x => x.CestaId == cestaAtiva.Id)
            .OrderBy(x => x.Ticker)
            .ToListAsync(ct);

        if (itensCesta.Count == 0)
        {
            throw new InvalidOperationException("CESTA_SEM_ITENS");
        }

        var contasFilhote = await EnsureFilhoteAccountsAsync(aportes.Select(x => x.Cliente).ToList(), contasRepo, ct);
        var contaMaster = await EnsureMasterAccountAsync(clientesRepo, contasRepo, ct);

        var totalConsolidado = Math.Round(aportes.Sum(x => x.Aporte), 2);
        var totalAportes = aportes.Sum(x => x.Aporte);

        var tickers = itensCesta.Select(x => x.Ticker.Trim().ToUpperInvariant()).Distinct().ToList();

        var quotesRaw = await cotacoesRepo.Query()
            .Where(x => tickers.Contains(x.Ticker) && x.DataPregao.Date <= referenceDate.ToDateTime(TimeOnly.MinValue).Date)
            .ToListAsync(ct);

        var quoteByTicker = quotesRaw
            .GroupBy(x => x.Ticker)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.DataPregao).First());

        foreach (var ticker in tickers)
        {
            if (!quoteByTicker.ContainsKey(ticker))
            {
                throw new InvalidOperationException($"COTACAO_NAO_ENCONTRADA:{ticker}");
            }
        }

        var masterCustodias = await custodiasRepo.Query()
            .Where(x => x.ContasGraficasId == contaMaster.Id)
            .ToListAsync(ct);

        var masterCustodiaByTicker = masterCustodias
            .Where(x => tickers.Contains(x.Ticker.Trim().ToUpperInvariant()))
            .ToDictionary(x => x.Ticker.Trim().ToUpperInvariant(), x => x);

        var orders = new List<OrderSummary>();
        var distributions = new List<ClientDistributionSummary>();
        var residuals = new List<ResidualSummary>();
        var irEvents = new List<PendingIrEvent>();

        var distMap = aportes.ToDictionary(
            x => x.Cliente.Id,
            x => new List<AssetQty>());

        await _uow.BeginAsync(ct);

        try
        {
            foreach (var item in itensCesta)
            {
                var ticker = item.Ticker.Trim().ToUpperInvariant();
                var quote = quoteByTicker[ticker].PrecoFechamento;
                if (quote <= 0m)
                {
                    continue;
                }

                var valorDestino = Math.Round(totalConsolidado * (item.Percentual / 100m), 2);
                var qtyAlvo = (int)Math.Floor(valorDestino / quote);

                var saldoMasterAnterior = masterCustodiaByTicker.TryGetValue(ticker, out var custMaster) ? custMaster.Quantidade : 0;
                var qtyComprar = Math.Max(qtyAlvo - saldoMasterAnterior, 0);

                if (qtyComprar > 0)
                {
                    var qtyLote = (qtyComprar / 100) * 100;
                    var qtyFracionario = qtyComprar - qtyLote;

                    if (qtyLote > 0)
                    {
                        await ordensRepo.AddAsync(new OrdensCompra
                        {
                            ContaMasterId = contaMaster.Id,
                            Ticker = ticker,
                            Quantidade = qtyLote,
                            PrecoUnitario = quote,
                            TipoMercado = TipoMercado.LOTE,
                            DataExecucao = DateTime.UtcNow,
                        }, ct);

                        orders.Add(new OrderSummary(ticker, qtyLote, quote, TipoMercado.LOTE));
                    }

                    if (qtyFracionario > 0)
                    {
                        await ordensRepo.AddAsync(new OrdensCompra
                        {
                            ContaMasterId = contaMaster.Id,
                            Ticker = ticker,
                            Quantidade = qtyFracionario,
                            PrecoUnitario = quote,
                            TipoMercado = TipoMercado.FRACIONARIO,
                            DataExecucao = DateTime.UtcNow,
                        }, ct);

                        orders.Add(new OrderSummary(ticker, qtyFracionario, quote, TipoMercado.FRACIONARIO));
                    }
                }

                var qtyDisponivel = saldoMasterAnterior + qtyComprar;
                var qtyDistribuidaTotal = 0;

                foreach (var aporte in aportes)
                {
                    var proporcao = totalAportes <= 0m ? 0m : aporte.Aporte / totalAportes;
                    var qtyCliente = (int)Math.Floor(qtyDisponivel * proporcao);
                    if (qtyCliente <= 0)
                    {
                        continue;
                    }

                    qtyDistribuidaTotal += qtyCliente;
                    distMap[aporte.Cliente.Id].Add(new AssetQty(ticker, qtyCliente));

                    var contaCliente = contasFilhote[aporte.Cliente.Id];
                    var custodiaCliente = await custodiasRepo.Query().FirstOrDefaultAsync(
                        x => x.ContasGraficasId == contaCliente.Id && x.Ticker == ticker, ct);

                    if (custodiaCliente is null)
                    {
                        await custodiasRepo.AddAsync(new Custodias
                        {
                            ContasGraficasId = contaCliente.Id,
                            Ticker = ticker,
                            Quantidade = qtyCliente,
                            PrecoMedio = quote,
                            DataUltimaAtualizacao = DateTime.UtcNow,
                        }, ct);
                    }
                    else
                    {
                        var qtdAnterior = custodiaCliente.Quantidade;
                        var qtdNova = qtdAnterior + qtyCliente;
                        if (qtdNova > 0)
                        {
                            custodiaCliente.PrecoMedio = Math.Round(((qtdAnterior * custodiaCliente.PrecoMedio) + (qtyCliente * quote)) / qtdNova, 6);
                        }

                        custodiaCliente.Quantidade = qtdNova;
                        custodiaCliente.DataUltimaAtualizacao = DateTime.UtcNow;
                        custodiasRepo.Update(custodiaCliente);
                    }

                    await distribuicoesRepo.AddAsync(new Distribuicoes
                    {
                        Ticker = ticker,
                        Valor = Math.Round(qtyCliente * quote, 2),
                        Data = DateTime.UtcNow,
                    }, ct);

                    var valorOperacao = Math.Round(qtyCliente * quote, 2);
                    var valorIr = Math.Round(valorOperacao * 0.00005m, 2);
                    var evt = new EventosIR
                    {
                        ClienteId = aporte.Cliente.Id,
                        Tipo = TipoIR.DEDO_DURO,
                        ValorBase = valorOperacao,
                        ValorIR = valorIr,
                        PublicadoKafka = false,
                        DataEvento = DateTime.UtcNow
                    };

                    await eventosIrRepo.AddAsync(evt, ct);
                    irEvents.Add(new PendingIrEvent(evt, aporte.Cliente.CPF, ticker));
                }

                var saldoMasterFinal = qtyDisponivel - qtyDistribuidaTotal;
                if (custMaster is null)
                {
                    if (saldoMasterFinal > 0)
                    {
                        var novaCustodiaMaster = new Custodias
                        {
                            ContasGraficasId = contaMaster.Id,
                            Ticker = ticker,
                            Quantidade = saldoMasterFinal,
                            PrecoMedio = quote,
                            DataUltimaAtualizacao = DateTime.UtcNow,
                        };

                        await custodiasRepo.AddAsync(novaCustodiaMaster, ct);
                        masterCustodiaByTicker[ticker] = novaCustodiaMaster;
                        residuals.Add(new ResidualSummary(ticker, saldoMasterFinal));
                    }
                }
                else
                {
                    var baseQty = custMaster.Quantidade;
                    var qtyPosCompra = baseQty + qtyComprar;

                    if (qtyPosCompra > 0 && qtyComprar > 0)
                    {
                        custMaster.PrecoMedio = Math.Round(((baseQty * custMaster.PrecoMedio) + (qtyComprar * quote)) / qtyPosCompra, 6);
                    }

                    custMaster.Quantidade = saldoMasterFinal;
                    custMaster.DataUltimaAtualizacao = DateTime.UtcNow;

                    if (saldoMasterFinal <= 0)
                    {
                        custodiasRepo.Remove(custMaster);
                    }
                    else
                    {
                        custodiasRepo.Update(custMaster);
                        residuals.Add(new ResidualSummary(ticker, saldoMasterFinal));
                    }
                }
            }

            await _uow.CommitAsync(ct);

            var irEventsPublished = await PublishAndMarkIrEventsAsync(irEvents, ct);
            await MarkExecutionSuccessAsync(execucoesRepo, execucao, ct);

            foreach (var aporte in aportes)
            {
                distributions.Add(new ClientDistributionSummary(
                    aporte.Cliente.Id,
                    aporte.Cliente.Nome,
                    aporte.Aporte,
                    distMap[aporte.Cliente.Id]));
            }

            return new ScheduledPurchaseResult(
                DateTimeOffset.UtcNow,
                referenceDate,
                aportes.Count,
                totalConsolidado,
                orders,
                distributions,
                residuals,
                irEventsPublished);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar motor de compra para {ReferenceDate}", referenceDate);
            await _uow.RollbackAsync(ct);
            await MarkExecutionFailureAsync(execucoesRepo, execucao, ex.Message, ct);
            throw;
        }
    }

    private async Task<MotorExecucao> StartExecutionAsync(IRepository<MotorExecucao> repo, DateOnly referenceDate, CancellationToken ct)
    {
        var dataRef = referenceDate.ToDateTime(TimeOnly.MinValue).Date;
        var now = DateTime.UtcNow;

        var existing = await repo.Query().FirstOrDefaultAsync(x => x.DataReferencia == dataRef, ct);
        if (existing is not null)
        {
            if (existing.Status is "SUCCESS" or "PENDING")
            {
                throw new InvalidOperationException("COMPRA_JA_EXECUTADA");
            }

            await _uow.BeginAsync(ct);
            existing.Status = "PENDING";
            existing.DataInicioUtc = now;
            existing.DataFimUtc = null;
            existing.Erro = null;
            repo.Update(existing);
            await _uow.CommitAsync(ct);
            return existing;
        }

        var novo = new MotorExecucao
        {
            DataReferencia = dataRef,
            Status = "PENDING",
            DataInicioUtc = now
        };

        try
        {
            await _uow.BeginAsync(ct);
            await repo.AddAsync(novo, ct);
            await _uow.CommitAsync(ct);
            return novo;
        }
        catch (DbUpdateException)
        {
            await _uow.RollbackAsync(ct);
            throw new InvalidOperationException("COMPRA_JA_EXECUTADA");
        }
    }

    private async Task MarkExecutionSuccessAsync(IRepository<MotorExecucao> repo, MotorExecucao execucao, CancellationToken ct)
    {
        await _uow.BeginAsync(ct);
        execucao.Status = "SUCCESS";
        execucao.DataFimUtc = DateTime.UtcNow;
        execucao.Erro = null;
        repo.Update(execucao);
        await _uow.CommitAsync(ct);
    }

    private async Task MarkExecutionFailureAsync(IRepository<MotorExecucao> repo, MotorExecucao execucao, string error, CancellationToken ct)
    {
        try
        {
            await _uow.BeginAsync(ct);
            execucao.Status = "FAILED";
            execucao.DataFimUtc = DateTime.UtcNow;
            execucao.Erro = error.Length > 1900 ? error[..1900] : error;
            repo.Update(execucao);
            await _uow.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao marcar execucao do motor como FAILED para {ReferenceDate}", execucao.DataReferencia);
        }
    }

    private async Task<int> PublishAndMarkIrEventsAsync(IReadOnlyList<PendingIrEvent> events, CancellationToken ct)
    {
        if (events.Count == 0) return 0;

        var eventosIrRepo = _uow.Repository<EventosIR>();
        var published = 0;

        foreach (var pending in events)
        {
            var evt = pending.Event;
            try
            {
                if (evt.Tipo == TipoIR.DEDO_DURO)
                {
                    await _publisher.PublishIrDedoDuroAsync(evt, pending.Cpf, pending.Ticker, ct);
                }
                else
                {
                    await _publisher.PublishIrVendaAsync(evt, pending.Cpf, pending.Ticker, ct);
                }

                await _uow.BeginAsync(ct);
                evt.PublicadoKafka = true;
                eventosIrRepo.Update(evt);
                await _uow.CommitAsync(ct);
                published++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao publicar evento IR {EventId} para cliente {ClientId}.", evt.Id, evt.ClienteId);
                throw new InvalidOperationException("KAFKA_INDISPONIVEL");
            }
        }

        return published;
    }

    private sealed record PendingIrEvent(EventosIR Event, string Cpf, string Ticker);

    private async Task<ContasGraficas> EnsureMasterAccountAsync(
        IRepository<Clientes> clientesRepo,
        IRepository<ContasGraficas> contasRepo,
        CancellationToken ct)
    {
        var contaMaster = await contasRepo.Query().FirstOrDefaultAsync(x => x.Tipo == TipoConta.Master, ct);
        if (contaMaster is not null)
        {
            return contaMaster;
        }

        var masterCliente = await clientesRepo.Query().FirstOrDefaultAsync(x => x.CPF == "99999999999", ct);
        if (masterCliente is null)
        {
            masterCliente = new Clientes
            {
                Nome = "Conta Master",
                CPF = "99999999999",
                Email = "master@local",
                ValorMensal = 0m,
                Ativo = false,
                DataAdesao = DateTime.UtcNow,
            };

            await clientesRepo.AddAsync(masterCliente, ct);
            await _uow.CommitAsync(ct);
        }

        contaMaster = new ContasGraficas
        {
            ClienteId = masterCliente.Id,
            NumeroConta = "MST-000001",
            Tipo = TipoConta.Master,
            DataCriacao = DateTime.UtcNow,
        };

        await contasRepo.AddAsync(contaMaster, ct);
        await _uow.CommitAsync(ct);

        return contaMaster;
    }

    private async Task<Dictionary<int, ContasGraficas>> EnsureFilhoteAccountsAsync(
        IReadOnlyList<Clientes> clientes,
        IRepository<ContasGraficas> contasRepo,
        CancellationToken ct)
    {
        var clienteIds = clientes.Select(x => x.Id).ToList();
        var contas = await contasRepo.Query()
            .Where(x => clienteIds.Contains(x.ClienteId) && x.Tipo == TipoConta.Filhote)
            .ToListAsync(ct);

        var map = contas.ToDictionary(x => x.ClienteId, x => x);
        var missing = clienteIds.Where(id => !map.ContainsKey(id)).ToList();

        if (missing.Count > 0)
        {
            foreach (var clienteId in missing)
            {
                var conta = new ContasGraficas
                {
                    ClienteId = clienteId,
                    NumeroConta = $"FLH-{clienteId:D6}",
                    Tipo = TipoConta.Filhote,
                    DataCriacao = DateTime.UtcNow,
                };

                await contasRepo.AddAsync(conta, ct);
            }

            await _uow.CommitAsync(ct);

            contas = await contasRepo.Query()
                .Where(x => clienteIds.Contains(x.ClienteId) && x.Tipo == TipoConta.Filhote)
                .ToListAsync(ct);

            map = contas.ToDictionary(x => x.ClienteId, x => x);
        }

        return map;
    }

}

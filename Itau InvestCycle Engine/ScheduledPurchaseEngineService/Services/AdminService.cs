using ClassLibrary.Contracts.DTOs;
using ClassLibrary.Contracts.DTOs.Admin;
using ClassLibrary.Domain.Entities;
using ClassLibrary.Domain.Entities.Cestas;
using ClassLibrary.Domain.Entities.Clientes;
using ClassLibrary.Domain.Entities.CompraDistribuicao;
using Itau.InvestCycleEngine.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using ScheduledPurchaseEngineService.Interfaces;

namespace ScheduledPurchaseEngineService.Services;

public sealed class AdminService : IAdminService
{
    private readonly IUnitOfWork _uow;
    private readonly IRebalanceService _rebalanceService;
    private readonly ILogger<AdminService> _logger;

    public AdminService(IUnitOfWork uow, IRebalanceService rebalanceService, ILogger<AdminService> logger)
    {
        _uow = uow;
        _rebalanceService = rebalanceService;
        _logger = logger;
    }

    public async Task<Result<CadastrarOuAlterarCestaResponse, ApiError>> CadastrarOuAlterarCestaAsync(CadastrarOuAlterarCestaRequest request, CancellationToken ct)
        => await SalvarNovaVersaoCestaAsync(request, expectedActiveCestaId: null, ct);

    public async Task<Result<CadastrarOuAlterarCestaResponse, ApiError>> EditarCestaAsync(int cestaId, CadastrarOuAlterarCestaRequest request, CancellationToken ct)
        => await SalvarNovaVersaoCestaAsync(request, expectedActiveCestaId: cestaId, ct);

    private async Task<Result<CadastrarOuAlterarCestaResponse, ApiError>> SalvarNovaVersaoCestaAsync(
        CadastrarOuAlterarCestaRequest request,
        int? expectedActiveCestaId,
        CancellationToken ct)
    {
        var validacao = await ValidateAndNormalizeItensAsync(request, ct);
        if (!validacao.IsSuccess)
            return Result<CadastrarOuAlterarCestaResponse, ApiError>.Failure(validacao.Err!);

        var normalizedItens = validacao.Ok!;

        var cestasRepo = _uow.Repository<CestasRecomendacao>();
        var itensCestaRepo = _uow.Repository<ItensCesta>();
        var clientesRepo = _uow.Repository<Clientes>();
        var now = DateTime.UtcNow;

        try
        {
            if (expectedActiveCestaId.HasValue)
            {
                var cestaSolicitada = await cestasRepo.Query()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == expectedActiveCestaId.Value, ct);

                if (cestaSolicitada is null)
                {
                    return Result<CadastrarOuAlterarCestaResponse, ApiError>.Failure(
                        new ApiError("Cesta nao encontrada.", "CESTA_NAO_ENCONTRADA"));
                }

                if (!cestaSolicitada.Ativa)
                {
                    return Result<CadastrarOuAlterarCestaResponse, ApiError>.Failure(
                        new ApiError("Apenas a cesta ativa pode ser alterada.", "CESTA_INATIVA"));
                }
            }

            var cestasAtivas = await cestasRepo.Query()
                .Where(x => x.Ativa)
                .ToListAsync(ct);

            var cestaAnterior = cestasAtivas
                .OrderByDescending(x => x.DataCriacao)
                .FirstOrDefault();

            if (expectedActiveCestaId.HasValue && cestaAnterior?.Id != expectedActiveCestaId.Value)
            {
                return Result<CadastrarOuAlterarCestaResponse, ApiError>.Failure(
                    new ApiError("A alteracao deve partir da cesta ativa atual.", "CESTA_INATIVA"));
            }

            var cestaAnteriorItens = cestaAnterior is null
                ? []
                : await itensCestaRepo.Query()
                    .AsNoTracking()
                    .Where(x => x.CestaId == cestaAnterior.Id)
                    .ToListAsync(ct);

            foreach (var cesta in cestasAtivas)
            {
                cesta.Ativa = false;
                cesta.DataDesativacao = now;
                cestasRepo.Update(cesta);
            }

            var novaCesta = new CestasRecomendacao
            {
                Nome = request.Nome,
                Ativa = true,
                DataCriacao = now,
                DataDesativacao = null
            };

            var itensNovos = normalizedItens
                .Select(x => new ItensCesta
                {
                    Cesta = novaCesta,
                    Ticker = x.Ticker,
                    Percentual = x.Percentual
                })
                .ToList();

            await _uow.BeginAsync(ct);

            await cestasRepo.AddAsync(novaCesta, ct);
            foreach (var item in itensNovos)
            {
                await itensCestaRepo.AddAsync(item, ct);
            }

            await _uow.CommitAsync(ct);

            var rebalanceamentoDisparado = cestaAnterior is not null;
            if (rebalanceamentoDisparado)
            {
                await _rebalanceService.RebalanceByBasketChangeAsync(cestaAnterior!.Id, novaCesta.Id, ct);
            }

            var tickersAnteriores = cestaAnteriorItens
                .Select(x => x.Ticker.Trim().ToUpperInvariant())
                .ToHashSet();

            var tickersNovos = itensNovos
                .Select(x => x.Ticker.Trim().ToUpperInvariant())
                .ToHashSet();

            var ativosRemovidos = tickersAnteriores.Except(tickersNovos).OrderBy(x => x).ToList();
            var ativosAdicionados = tickersNovos.Except(tickersAnteriores).OrderBy(x => x).ToList();
            var percentualAnteriorPorTicker = cestaAnteriorItens
                .GroupBy(x => x.Ticker.Trim().ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.First().Percentual);
            var percentualNovoPorTicker = itensNovos
                .GroupBy(x => x.Ticker.Trim().ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.First().Percentual);
            var ativosPercentualAlterado = percentualAnteriorPorTicker.Keys
                .Intersect(percentualNovoPorTicker.Keys)
                .Select(ticker => new AtivoPercentualAlteradoResponse(
                    Ticker: ticker,
                    PercentualAnterior: percentualAnteriorPorTicker[ticker],
                    PercentualNovo: percentualNovoPorTicker[ticker]))
                .Where(x => Math.Abs(x.PercentualAnterior - x.PercentualNovo) > 0.0001m)
                .OrderBy(x => x.Ticker)
                .ToList();

            var totalClientesAtivos = await clientesRepo.Query().CountAsync(x => x.Ativo, ct);

            var mensagem = rebalanceamentoDisparado
                ? $"Cesta atualizada. Rebalanceamento disparado para {totalClientesAtivos} clientes ativos."
                : "Primeira cesta cadastrada com sucesso.";

            return Result<CadastrarOuAlterarCestaResponse, ApiError>.Success(new CadastrarOuAlterarCestaResponse(
                CestaId: novaCesta.Id,
                Nome: novaCesta.Nome,
                Ativa: novaCesta.Ativa,
                DataCriacao: novaCesta.DataCriacao,
                Itens: itensNovos.Select(x => new CestaItemResponse(x.Ticker, x.Percentual)).ToList(),
                CestaAnteriorDesativada: cestaAnterior is null
                    ? null
                    : new CestaAnteriorDesativadaResponse(cestaAnterior.Id, cestaAnterior.Nome, now),
                RebalanceamentoDisparado: rebalanceamentoDisparado,
                AtivosRemovidos: ativosRemovidos,
                AtivosAdicionados: ativosAdicionados,
                Mensagem: mensagem,
                AtivosPercentualAlterado: ativosPercentualAlterado));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao cadastrar/alterar cesta.");
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<TickersDisponiveisResponse> ListarTickersDisponiveisAsync(string? query, int limit, CancellationToken ct)
    {
        var safeLimit = Math.Clamp(limit, 1, 2000);
        var normalizedQuery = NormalizeTicker(query);

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

        var filtrados = await tickersQuery
            .OrderBy(x => x)
            .Take(safeLimit)
            .ToListAsync(ct);

        return new TickersDisponiveisResponse(filtrados);
    }

    public async Task<Result<bool, ApiError>> ExcluirCestaAsync(int cestaId, CancellationToken ct)
    {
        var cestasRepo = _uow.Repository<CestasRecomendacao>();
        var itensCestaRepo = _uow.Repository<ItensCesta>();

        var cesta = await cestasRepo.Query()
            .FirstOrDefaultAsync(x => x.Id == cestaId, ct);

        if (cesta is null)
        {
            return Result<bool, ApiError>.Failure(
                new ApiError("Cesta nao encontrada.", "CESTA_NAO_ENCONTRADA"));
        }

        try
        {
            await _uow.BeginAsync(ct);

            var itens = await itensCestaRepo.Query()
                .Where(x => x.CestaId == cestaId)
                .ToListAsync(ct);

            foreach (var item in itens)
            {
                itensCestaRepo.Remove(item);
            }

            cestasRepo.Remove(cesta);
            await _uow.CommitAsync(ct);

            return Result<bool, ApiError>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao excluir cesta {CestaId}.", cestaId);
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<Result<CestaAtualResponse, ApiError>> ConsultarCestaAtualAsync(CancellationToken ct)
    {
        var cestasRepo = _uow.Repository<CestasRecomendacao>();
        var itensCestaRepo = _uow.Repository<ItensCesta>();
        var cotacoesRepo = _uow.Repository<Cotacoes>();

        var cesta = await cestasRepo.Query()
            .AsNoTracking()
            .Where(x => x.Ativa)
            .OrderByDescending(x => x.DataCriacao)
            .FirstOrDefaultAsync(ct);

        if (cesta is null)
        {
            return Result<CestaAtualResponse, ApiError>.Failure(new ApiError("Nenhuma cesta ativa encontrada.", "CESTA_NAO_ENCONTRADA"));
        }

        var itens = await itensCestaRepo.Query()
            .AsNoTracking()
            .Where(x => x.CestaId == cesta.Id)
            .OrderBy(x => x.Ticker)
            .ToListAsync(ct);

        var tickers = itens.Select(x => x.Ticker.Trim().ToUpperInvariant()).Distinct().ToList();

        var cotacoes = await cotacoesRepo.Query()
            .AsNoTracking()
            .Where(x => tickers.Contains(x.Ticker))
            .ToListAsync(ct);

        var cotacaoAtualPorTicker = cotacoes
            .GroupBy(x => x.Ticker.Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.DataPregao).First().PrecoFechamento);

        var response = new CestaAtualResponse(
            CestaId: cesta.Id,
            Nome: cesta.Nome,
            Ativa: cesta.Ativa,
            DataCriacao: cesta.DataCriacao,
            Itens: itens.Select(x =>
            {
                var ticker = x.Ticker.Trim().ToUpperInvariant();
                var cotacaoAtual = cotacaoAtualPorTicker.TryGetValue(ticker, out var quote) ? quote : 0m;
                return new CestaAtualItemResponse(ticker, x.Percentual, cotacaoAtual);
            }).ToList());

        return Result<CestaAtualResponse, ApiError>.Success(response);
    }

    public async Task<HistoricoCestasResponse> HistoricoCestasAsync(CancellationToken ct)
    {
        var cestasRepo = _uow.Repository<CestasRecomendacao>();
        var itensCestaRepo = _uow.Repository<ItensCesta>();

        var cestas = await cestasRepo.Query()
            .AsNoTracking()
            .OrderByDescending(x => x.DataCriacao)
            .ToListAsync(ct);

        var cestaIds = cestas.Select(x => x.Id).ToList();

        var itens = await itensCestaRepo.Query()
            .AsNoTracking()
            .Where(x => cestaIds.Contains(x.CestaId))
            .OrderBy(x => x.Ticker)
            .ToListAsync(ct);

        var itensPorCesta = itens
            .GroupBy(x => x.CestaId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return new HistoricoCestasResponse(cestas.Select(cesta =>
        {
            itensPorCesta.TryGetValue(cesta.Id, out var itensCesta);

            return new CestaHistoricoItemResponse(
                CestaId: cesta.Id,
                Nome: cesta.Nome,
                Ativa: cesta.Ativa,
                DataCriacao: cesta.DataCriacao,
                DataDesativacao: cesta.DataDesativacao,
                Itens: (itensCesta ?? [])
                    .Select(x => new CestaItemResponse(x.Ticker.Trim().ToUpperInvariant(), x.Percentual))
                    .ToList());
        }).ToList());
    }

    public async Task<Result<ContaMasterCustodiaResponse, ApiError>> ConsultarCustodiaMasterAsync(CancellationToken ct)
    {
        var contaMasterRepo = _uow.Repository<ContaMaster>();
        var contasRepo = _uow.Repository<ContasGraficas>();
        var custodiasRepo = _uow.Repository<Custodias>();
        var cotacoesRepo = _uow.Repository<Cotacoes>();
        var ordensRepo = _uow.Repository<OrdensCompra>();
        var precosMediosRepo = _uow.Repository<PrecoMedio>();

        var contaMasterRegistro = await contaMasterRepo.Query()
            .AsNoTracking()
            .Include(x => x.ContaGrafica)
            .FirstOrDefaultAsync(ct);

        var contaMaster = contaMasterRegistro?.ContaGrafica;

        if (contaMaster is null)
        {
            contaMaster = await contasRepo.Query()
                .AsNoTracking()
                .Where(x => x.Tipo == TipoConta.Master)
                .FirstOrDefaultAsync(ct);
        }

        if (contaMaster is null)
        {
            return Result<ContaMasterCustodiaResponse, ApiError>.Failure(new ApiError("Conta master nao encontrada.", "CONTA_MASTER_NAO_ENCONTRADA"));
        }

        var custodias = await custodiasRepo.Query()
            .AsNoTracking()
            .Where(x => x.ContasGraficasId == contaMaster.Id && x.Quantidade > 0)
            .OrderBy(x => x.Ticker)
            .ToListAsync(ct);

        var tickers = custodias
            .Select(x => x.Ticker.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        var cotacoes = await cotacoesRepo.Query()
            .AsNoTracking()
            .Where(x => tickers.Contains(x.Ticker))
            .ToListAsync(ct);

        var cotacaoAtualPorTicker = cotacoes
            .GroupBy(x => x.Ticker.Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.DataPregao).First().PrecoFechamento);

        var ordensMasterPendentes = await ordensRepo.Query()
            .AsNoTracking()
            .Where(x => x.ContaMasterId == contaMaster.Id)
            .OrderByDescending(x => x.DataExecucao)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);

        var ordensPendentesPorTicker = ordensMasterPendentes
            .GroupBy(x => NormalizeOrderTicker(x.Ticker))
            .ToDictionary(g => g.Key, g => g.ToList());

        var custodiaIds = custodias.Select(x => x.Id).ToList();
        var precoMedioMap = await precosMediosRepo.Query()
            .AsNoTracking()
            .Where(x => custodiaIds.Contains(x.CustodiaId))
            .ToDictionaryAsync(x => x.CustodiaId, x => x.Valor, ct);

        var custodiaResponse = custodias.Select(c =>
        {
            var ticker = c.Ticker.Trim().ToUpperInvariant();
            var precoMedio = precoMedioMap.TryGetValue(c.Id, out var valorPersistido) ? valorPersistido : c.PrecoMedio;
            var cotacaoAtual = cotacaoAtualPorTicker.TryGetValue(ticker, out var quote) ? quote : precoMedio;
            var valorAtual = Math.Round(cotacaoAtual * c.Quantidade, 2);
            var origem = ordensPendentesPorTicker.TryGetValue(ticker, out var ordensTicker) && ordensTicker.Count > 0
                ? ordensTicker.Count == 1
                    ? $"Residuo da ordem {ordensTicker[0].Id} de {ordensTicker[0].DataExecucao:yyyy-MM-dd}"
                    : $"Residuos originados por {ordensTicker.Count} ordens"
                : $"Residuo distribuicao {c.DataUltimaAtualizacao:yyyy-MM-dd}";

            return new CustodiaMasterItemResponse(
                Ticker: ticker,
                Quantidade: c.Quantidade,
                PrecoMedio: precoMedio,
                ValorAtual: valorAtual,
                Origem: origem);
        }).ToList();

        return Result<ContaMasterCustodiaResponse, ApiError>.Success(new ContaMasterCustodiaResponse(
            ContaMaster: new ContaMasterInfoResponse(
                contaMaster.Id,
                contaMaster.NumeroConta,
                contaMaster.Tipo.ToString().ToUpperInvariant()),
            Custodia: custodiaResponse,
            ValorTotalResiduo: Math.Round(custodiaResponse.Sum(x => x.ValorAtual), 2)));
    }

    public async Task<Result<RebalanceamentoDesvioResponse, ApiError>> RebalancearPorDesvioAsync(RebalanceamentoDesvioRequest request, CancellationToken ct)
    {
        if (request.ThresholdPercentual <= 0m || request.ThresholdPercentual > 100m)
        {
            return Result<RebalanceamentoDesvioResponse, ApiError>.Failure(
                new ApiError("Threshold percentual deve estar entre 0 e 100.", "THRESHOLD_INVALIDO"));
        }

        var cestaAtiva = await _uow.Repository<CestasRecomendacao>()
            .Query()
            .AsNoTracking()
            .AnyAsync(x => x.Ativa, ct);

        if (!cestaAtiva)
        {
            return Result<RebalanceamentoDesvioResponse, ApiError>.Failure(
                new ApiError("Nenhuma cesta ativa encontrada.", "CESTA_NAO_ENCONTRADA"));
        }

        var (evaluated, rebalanced) = await _rebalanceService.RebalanceByDriftAsync(request.ThresholdPercentual, ct);

        return Result<RebalanceamentoDesvioResponse, ApiError>.Success(new RebalanceamentoDesvioResponse(
            TotalClientesAvaliados: evaluated,
            TotalClientesRebalanceados: rebalanced,
            ThresholdPercentual: request.ThresholdPercentual,
            Mensagem: $"Rebalanceamento por desvio executado. Clientes rebalanceados: {rebalanced}/{evaluated}."));
    }

    private async Task<Result<IReadOnlyList<CestaItemRequest>, ApiError>> ValidateAndNormalizeItensAsync(
        CadastrarOuAlterarCestaRequest request,
        CancellationToken ct)
    {
        if (request.Itens.Count != 5)
        {
            return Result<IReadOnlyList<CestaItemRequest>, ApiError>.Failure(new ApiError(
                $"A cesta deve conter exatamente 5 ativos. Quantidade informada: {request.Itens.Count}.",
                "QUANTIDADE_ATIVOS_INVALIDA"));
        }

        if (request.Itens.Any(x => x.Percentual <= 0m))
        {
            return Result<IReadOnlyList<CestaItemRequest>, ApiError>.Failure(new ApiError(
                "Cada percentual da cesta deve ser maior que 0%.",
                "PERCENTUAIS_INVALIDOS"));
        }

        var normalizedItens = request.Itens
            .Select(x => new CestaItemRequest(NormalizeTicker(x.Ticker), x.Percentual))
            .ToList();

        var normalizedTickers = normalizedItens
            .Select(x => x.Ticker)
            .ToList();

        if (normalizedTickers.Any(string.IsNullOrWhiteSpace) || normalizedTickers.Distinct().Count() != 5)
        {
            return Result<IReadOnlyList<CestaItemRequest>, ApiError>.Failure(new ApiError(
                "A cesta deve conter 5 tickers validos e sem repeticao.",
                "QUANTIDADE_ATIVOS_INVALIDA"));
        }

        var somaPercentuais = normalizedItens.Sum(x => x.Percentual);
        if (Math.Abs(somaPercentuais - 100m) > 0.0001m)
        {
            return Result<IReadOnlyList<CestaItemRequest>, ApiError>.Failure(new ApiError(
                $"A soma dos percentuais deve ser exatamente 100%. Soma atual: {somaPercentuais}%.",
                "PERCENTUAIS_INVALIDOS"));
        }

        var tickersExistentes = await _uow.Repository<Cotacoes>()
            .Query()
            .AsNoTracking()
            .Where(x => normalizedTickers.Contains(x.Ticker.Trim().ToUpper()))
            .Select(x => x.Ticker)
            .Distinct()
            .ToListAsync(ct);

        var tickersExistentesNormalizados = tickersExistentes
            .Select(NormalizeTicker)
            .ToHashSet();

        var tickersInvalidos = normalizedTickers
            .Where(x => !tickersExistentesNormalizados.Contains(x))
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (tickersInvalidos.Count > 0)
        {
            return Result<IReadOnlyList<CestaItemRequest>, ApiError>.Failure(new ApiError(
                $"Ticker(s) invalido(s): {string.Join(", ", tickersInvalidos)}.",
                "TICKER_INVALIDO"));
        }

        return Result<IReadOnlyList<CestaItemRequest>, ApiError>.Success(normalizedItens);
    }

    private static string NormalizeTicker(string? ticker)
        => (ticker ?? string.Empty).Trim().ToUpperInvariant();

    private static string NormalizeOrderTicker(string? ticker)
    {
        var normalized = NormalizeTicker(ticker);
        return normalized.EndsWith('F') ? normalized[..^1] : normalized;
    }
}
